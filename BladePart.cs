using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;
using ExtensionMethods;
using System;
using Shatterblade.Modes;
using Random = UnityEngine.Random;

namespace Shatterblade {
    public class BladePart : MonoBehaviour {
        const float LOCKED_DRAG = 0.01f;
        const float LOCKED_ANGULAR_DRAG = 0.05f;

        public ConfigurableJoint joint;
        public Shatterblade sword;
        public Rigidbody targetPoint;
        public Item item;
        float lastUnlockTime;
        public bool wasInHolder;
        private Vector3 lastSwordScale;
        private float lastUngrab = 0f;

        public PartState state;

        Quaternion lastTargetRotationDelta = Quaternion.identity;
        Vector3 lastTargetPositionDelta = Vector3.zero;
        private bool isFlashing;

        public void Awake() {
            item = GetComponent<Item>();
            item.disallowDespawn = true;
            item.OnGrabEvent += (handle, hand) => Detach();
            item.OnUngrabEvent += (handle, hand, throwing) => lastUngrab = Time.time;
            item.OnDespawnEvent += () => {
                targetPoint = null;
                sword.parts.Remove(this);
            };
            item.colliderGroups.ForEach(cg => cg.data.modifiers.ForEach(modifier => modifier.imbueRate = 2.0f));
            item.mainHandleLeft.touchRadius = 0.1f;
        }

        public void DeInit() {
            Detach();
            Destroy(this);
        }

        /// <summary>
        /// Toggle collision
        /// </summary>
        public void SetCollision(bool enable) {
            foreach (var collider in item.colliderGroups.First().colliders) {
                collider.enabled = enable;
            }
            //item.collisionHandlers.First().enabled = enable;
        }
        public void IgnoreRagdoll(Ragdoll ragdoll, bool ignore) {
            foreach (var part in ragdoll.parts)
                foreach (var cg in item.colliderGroups)
                    foreach (var thisCollider in cg.colliders)
                        foreach (var collider in part.colliderGroup.colliders)
                            Physics.IgnoreCollision(thisCollider, collider, ignore);
        }
        public void IgnoreHand(RagdollHand hand, bool ignore, float delay = 0) {
            this.RunAfter(() => {
                if (item.mainHandler == hand) return;
                foreach (var otherCollider in hand.colliderGroup.colliders) {
                    foreach (var cg in item.colliderGroups) {
                        foreach (var collider in cg.colliders) {
                            Physics.IgnoreCollision(collider, otherCollider, ignore);
                        }
                    }
                }

                foreach (var otherCollider in hand.lowerArmPart.colliderGroup.colliders) {
                    foreach (var cg in item.colliderGroups) {
                        foreach (var collider in cg.colliders) {
                            Physics.IgnoreCollision(collider, otherCollider, ignore);
                        }
                    }
                }

                foreach (var otherCollider in hand.upperArmPart.colliderGroup.colliders) {
                    foreach (var cg in item.colliderGroups) {
                        foreach (var collider in cg.colliders) {
                            Physics.IgnoreCollision(collider, otherCollider, ignore);
                        }
                    }
                }
            }, delay);
        }

        /// <summary>
        /// Initialise the shatterblade part
        /// </summary>
        public void Init(Shatterblade sword, Rigidbody targetPoint) {
            this.sword = sword;
            wasInHolder = sword.item.holder != null;
            this.targetPoint = targetPoint;
            ForAllRenderers(renderer => {
                renderer.material.SetVector("WorldPos", item.transform.position);
                renderer.material.SetFloat("SpawnAmount", 0);
            });
            SetCollision(false);
            item.mainHandleLeft.handlers.ForEach(handler => handler?.UnGrab(false));
            item.mainHandleLeft.SetTouch(false);
            lastTargetRotationDelta = Quaternion.Inverse(sword.transform.rotation) * targetPoint.rotation;
            lastTargetPositionDelta = sword.transform.InverseTransformPoint(targetPoint.position);
            item.collisionHandlers.FirstOrDefault().enabled = false;
            item.collisionHandlers.FirstOrDefault().OnCollisionStartEvent += collision => {
                if (IsLocked()) {
                    sword.BladeHaptic(collision.impactVelocity.magnitude);
                }
            };
            Show();
        }
        public void ForAllRenderers(Action<MeshRenderer> action) {
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>()) action(renderer);
        }
        public IEnumerator ShowCoroutine() {
            item.gameObject.SetActive(true);
            ForAllRenderers(renderer => renderer.material.SetFloat("SpawnAmount", 0));
            float startTime = Time.time;
            while (Time.time - startTime < 0.5f) {
                float ratio = (Time.time - startTime) / 0.5f;
                ForAllRenderers(renderer => renderer.material.SetFloat("SpawnAmount", ratio));
                yield return 0;
            }
            ForAllRenderers(renderer => renderer.material.SetFloat("SpawnAmount", 1));
            SetCollision(true);
            item.collisionHandlers.FirstOrDefault().enabled = true;
            item.mainHandleLeft.SetTouch(true);
        }
        public IEnumerator HideCoroutine() {
            float startTime = Time.time;
            ForAllRenderers(renderer => renderer.material.SetFloat("SpawnAmount", 1));
            while (Time.time - startTime < 0.5f) {
                float ratio = 1 - (Time.time - startTime) / 0.5f;
                ForAllRenderers(renderer => renderer.material.SetFloat("SpawnAmount", ratio));
                yield return 0;
            }
            ForAllRenderers(renderer => renderer.material.SetFloat("SpawnAmount", 0));
            SetCollision(false);
            item.mainHandleLeft.handlers.ForEach(handler => handler?.UnGrab(false));
            item.mainHandleLeft.SetTouch(false);
        }

        // State checks
        public bool IsLocked() => state == PartState.Locked;
        public bool IsFree() => state == PartState.Free;
        public bool IsReforming() => state == PartState.Reforming;

        /// <summary>
        /// Start the spawn effect coroutine
        /// </summary>
        public void Show() => StartCoroutine(ShowCoroutine());

        /// <summary>
        /// Start the hide effect coroutine
        /// </summary>
        public void Hide() => StartCoroutine(HideCoroutine());

        public void Flash() => StartCoroutine(FlashCoroutine());
        public IEnumerator FlashCoroutine() {
            if (isFlashing) yield break;
            isFlashing = true;
            float start = Time.time;
            var initialEmissive = new Dictionary<Renderer, Color>();
            item.renderers.ForEach(renderer
                => initialEmissive[renderer] = renderer.material.GetColor("_EmissionColor"));
            const float duration = 0.5f;
            Color targetColor = Color.HSVToRGB(Random.Range(0f, 1f), 1, 100, true);
            while (Time.time - start < duration) {
                float value = Mathf.Sin(Mathf.PI * (Time.time - start) / duration);
                item.renderers.ForEach(renderer => {
                    renderer.material.SetColor("_EmissionColor",
                        Color.Lerp(initialEmissive[renderer], new Color(8, 0, 20),
                            Mathf.Sin(Mathf.PI * (Time.time - start) / duration)));
                    //renderer.material.SetFloat("_Smoothness", Mathf.Lerp(0.5f, 1, value));
                    //renderer.material.SetFloat("_OcclusionStrength", Mathf.Lerp(0.5f, 1, value));
                });
                yield return 0;
            }
            isFlashing = false;
            item.renderers.ForEach(renderer => renderer.material.SetColor("_EmissionColor", initialEmissive[renderer]));
        }

        /// <summary>
        /// Part update loop
        /// </summary>
        public void Update() {
            try {
                // code that checks if the sword is despawned
                if (sword?.gameObject?.transform == null) {
                    sword = null;
                }
            } catch (NullReferenceException) {
                // code that runs if the despawn check breaks
                state = PartState.Free;
                item.mainHandleLeft.SetTelekinesis(true);
                item.mainCollisionHandler.RemovePhysicModifier(this);
                DestroyJoint();
                Destroy(this);
            }

            if (sword == null) {
                return;
            }

            if (sword.item.holder == null && sword.item.transform.localScale != lastSwordScale) {
                item.transform.localScale = sword.item.transform.localScale;
                lastSwordScale = sword.item.transform.localScale;
                Detach();
                Reform();
            }

            ForAllRenderers(renderer => renderer.material.SetVector("WorldPos", item.transform.position));
            if (Time.time - lastUngrab > 1f)
                item.SetColliderLayer(GameManager.GetLayer(LayerName.PlayerHandAndFoot));
            item.SetMeshLayer(GameManager.GetLayer(LayerName.MovingObject));
            //if (state == PartState.Reforming) {
            //    item.SetColliderAndMeshLayer(GameManager.GetLayer(LayerName.NPCGrabbedObject));
            //} else {
            //}
            switch (state) {
                case PartState.Free:
                    break;
                case PartState.Reforming:
                    break;
                case PartState.Locked:
                    break;
            }
            if (!targetPoint)
                return;
            if (joint && !IsLocked() && sword.ShouldReform(this)) {
                SetDriveFactor(Mathf.InverseLerp(2, 0, Vector3.Distance(transform.position, targetPoint.transform.position)));
                if (Vector3.Distance(transform.position, targetPoint.transform.position) > 6) {
                    transform.position = Vector3.Lerp(
                        transform.position,
                        targetPoint.transform.position,
                        Time.deltaTime * Mathf.Sqrt(Vector3.Distance(transform.position, targetPoint.transform.position) - 6));
                }
                item.rb.velocity = Vector3.Lerp(
                    item.rb.velocity,
                    Vector3.Project(item.rb.velocity, transform.position - targetPoint.transform.position),
                    Time.deltaTime * 6);
            }
            if (joint && IsLocked())
                if (IsLocked()) {
                    if (Vector3.Distance(transform.position, targetPoint.transform.position) > 0.5f) {
                        transform.position = Vector3.Lerp(
                            transform.position,
                            targetPoint.transform.position,
                            Time.deltaTime * Mathf.Sqrt(Vector3.Distance(transform.position, targetPoint.transform.position)));
                    }
                    if (lastTargetRotationDelta != Quaternion.Inverse(sword.transform.rotation) * targetPoint.rotation) {
                        RefreshJoint();
                        lastTargetRotationDelta = Quaternion.Inverse(sword.transform.rotation) * targetPoint.rotation;
                    }
                    if (lastTargetPositionDelta != sword.transform.InverseTransformPoint(targetPoint.position)) {
                        RefreshJoint();
                        lastTargetPositionDelta = sword.transform.InverseTransformPoint(targetPoint.position);
                    }
                }
            if (sword.ShouldReform(this)) {
                // If sword is locking and holstered
                if (!item.holder) {
                    if (sword.item.holder != null && IsLocked()) {
                        if (!wasInHolder) {
                            wasInHolder = true;
                            item.rb.isKinematic = true;
                            item.transform.position = targetPoint.transform.position;
                            item.transform.rotation = targetPoint.transform.rotation;
                            item.transform.SetParent(targetPoint.transform);
                            Hide();
                        }
                    } else {
                        if (wasInHolder) {
                            wasInHolder = false;
                            item.rb.isKinematic = false;
                            item.transform.SetParent(null);
                            Show();
                            Reform();
                        }
                    }
                }
                if (!IsLocked() && Time.time - lastUnlockTime > 0.1f) {
                    if (!joint) {
                        if (!item.mainHandler) {
                            Reform();
                        }
                    }
                    if (Vector3.Distance(transform.position, targetPoint.transform.position) < 0.1f
                     && Quaternion.Angle(transform.rotation, targetPoint.transform.rotation) < 10f
                     && sword.ShouldPartLock(this)) {
                        state = PartState.Locked;
                        item.mainHandleLeft.SetTelekinesis(false);
                        if (!(sword.mode is CannonMode))
                            sword.item.handlers.ForEach(handler => handler.HapticTick());
                        //Flash();
                        //Catalog.GetData<EffectData>("ShatterbladeClink").Spawn(transform).Play();
                        Attach();
                    }
                }
            }
        }

        public void Depenetrate() {
            item.mainCollisionHandler.damagers.ForEach(damager => damager.UnPenetrateAll());
        }

        public void RefreshJoint() {
            Quaternion orgRotation = transform.rotation;
            transform.rotation = targetPoint.transform.rotation;
            joint.autoConfigureConnectedAnchor = false;
            joint.targetRotation = Quaternion.identity;
            joint.anchor = Vector3.zero;
            if (sword.shouldLock) {
                joint.connectedAnchor = sword.transform.InverseTransformPoint(targetPoint.transform.position);
                joint.connectedBody = sword.item.rb;
                joint.massScale = 20f;
            } else {
                joint.connectedAnchor = Vector3.zero;
                joint.connectedBody = targetPoint;
            }
            transform.rotation = orgRotation;
        }

        /// <summary>
        /// Attach the part to the blade's rigidbody
        /// </summary>
        public void Attach() {
            item.mainCollisionHandler.SetPhysicModifier(this, 4, 0, 1, LOCKED_DRAG, LOCKED_ANGULAR_DRAG);
            //Catalog.GetData<EffectData>("ShatterbladeSnick").Spawn(transform).Play();
            LockJoint();
            Depenetrate();
            DestroyJoint();
            CreateJoint(true);
        }

        /// <summary>
        /// Detach the part from the blade's rigidbody
        /// </summary>
        public void Detach(bool shouldThrow = false) {
            state = PartState.Free;
            item.mainHandleLeft.SetTelekinesis(true);
            item.mainCollisionHandler.RemovePhysicModifier(this);
            if (shouldThrow)
                item.rb.velocity = sword.item.rb.GetPointVelocity(targetPoint.transform.position) * 3f;
            DestroyJoint();
        }

        /// <summary>
        /// Start to move the part toward the part's target position
        /// </summary>
        public void Reform() {
            // if (item.mainHandler == null)
            //     return;
            state = PartState.Reforming;
            item.mainHandleLeft.SetTelekinesis(false);
            Depenetrate();
            item.mainCollisionHandler.SetPhysicModifier(this, 4, 0);
            DestroyJoint();
            CreateJoint();
        }

        public void LockJoint() {
            JointDrive posDrive = joint.xDrive;
            posDrive.positionSpring = 1000000;
            posDrive.positionDamper = 4000;
            posDrive.maximumForce = Mathf.Infinity;
            joint.xDrive = posDrive;
            joint.yDrive = posDrive;
            joint.zDrive = posDrive;
            sword.ModifyJoint(this);
        }

        /// <summary>
        /// Scale drive spring and damping to a given factor
        /// </summary>
        /// <param name="factor">The factor to scale to</param>
        public void SetDriveFactor(float factor) {
            //item.mainCollisionHandler.SetPhysicModifier(this, 4, 0, 1, 0.1f);
            JointDrive posDrive = joint.xDrive;
            posDrive.positionSpring = 2000;
            posDrive.positionDamper = Mathf.Lerp(20, 100, factor);
            posDrive.maximumForce = Mathf.Infinity;
            joint.xDrive = posDrive;
            joint.yDrive = posDrive;
            joint.zDrive = posDrive;
            sword.ModifyJoint(this);
        }

        /// <summary>
        /// Create a joint attaching the part to the blade
        /// </summary>
        public void CreateJoint(bool toSword = false) {
            if (joint) {
                return;
            }
            Quaternion orgRotation = transform.rotation;
            transform.rotation = targetPoint.transform.rotation;
            joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.targetRotation = Quaternion.identity;
            joint.anchor = Vector3.zero;
            if (toSword && sword.shouldLock) {
                joint.connectedAnchor = sword.transform.InverseTransformPoint(targetPoint.transform.position);
                joint.connectedBody = sword.item.rb;
                joint.massScale = 20f;
            } else {
                joint.connectedAnchor = Vector3.zero;
                joint.connectedBody = targetPoint;
            }
            JointDrive posDrive = new JointDrive {
                positionSpring = 2000,
                positionDamper = 120,
                maximumForce = Mathf.Infinity
            };
            JointDrive rotDrive = new JointDrive {
                positionSpring = 1000,
                positionDamper = 10,
                maximumForce = Mathf.Infinity
            };
            joint.rotationDriveMode = RotationDriveMode.XYAndZ;
            joint.xDrive = posDrive;
            joint.yDrive = posDrive;
            joint.zDrive = posDrive;
            joint.angularXDrive = rotDrive;
            joint.angularYZDrive = rotDrive;
            transform.rotation = orgRotation;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            sword.ModifyJoint(this);
        }

        /// <summary>
        /// Destroy the joint if it exists
        /// </summary>
        public void DestroyJoint() {
            if (joint) {
                Destroy(joint);
                lastUnlockTime = Time.time;
                joint = null;
            }
        }

        public float DistanceToClosestHand()
            => Mathf.Min(Vector3.Distance(transform.position, Player.currentCreature.GetHand(Side.Left).Palm()),
                Vector3.Distance(transform.position, Player.currentCreature.GetHand(Side.Left).Palm()));
    }
    public enum PartState {
        Free,
        Reforming,
        Locked
    }
}
