﻿using System.Collections;
using System.Linq;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;
using Object = UnityEngine.Object;

namespace Shatterblade {
    class GravityMode : ImbueMode {
        float rotation;
        Item targetItem;
        ConfigurableJoint joint;
        Rigidbody targetPoint;
        float effectIntensity;
        EffectInstance effect;

        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            targetPoint = new GameObject().AddComponent<Rigidbody>();
            targetPoint.useGravity = false;
            targetPoint.isKinematic = true;
            targetPoint.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        }
        public void AcquireTarget() {
            var items = from item in Utils.ConeCastItem(sword.GetRB(1).transform.position, 5, ForwardDir(), 30, 50)
                        where item.GetComponent<BladePart>() == null && item != sword.item && !item.rb.isKinematic
                        orderby Vector3.Distance(item.transform.position, sword.GetRB(1).transform.position) / 30 * 2
                                * Vector3.Angle(ForwardDir(), item.transform.position - sword.GetRB(1).transform.position) / 50
                        select item;

            if (items.FirstOrDefault() is Item itemTarget) {
                targetItem = itemTarget;
                targetItem.Depenetrate();
                targetItem.collisionHandlers.ForEach(ch => ch.SetPhysicModifier(this, 4, 0, 1, 5));
                Object.Destroy(joint);
                joint = Utils.CreateSimpleJoint(targetItem.rb, targetPoint, 100 * targetItem.GetMassModifier(), 5 * targetItem.GetMassModifier());
                effect = Catalog.GetData<EffectData>("ShatterbladeGravity").Spawn(HeldCenter(), targetItem.transform.rotation, null, null, false);
                effect.SetPosition(HeldCenter());
                effect.Play();
                return;
            }
        }
        public void ThrowHeldThing() {
            if (targetItem) {
                targetItem.Throw(1, Item.FlyDetection.Forced);
                targetItem.rb.AddForce(Utils.HomingThrow(targetItem, ForwardDir(), 30) * targetItem.GetMassModifier() * 30, ForceMode.Impulse);
                Catalog.GetData<EffectData>("ShatterbladeGravityFire")
                    .Spawn(targetItem.transform.position, targetItem.transform.rotation).Play();
                sword.StartCoroutine(ThrowEffectCoroutine());
            }
        }
        void UpdateEffectParams(EffectInstance effect, float intensity, Vector3 position, Quaternion rotation) {
            effect.SetIntensity(intensity);
            effect.SetScale(Vector3.one * HeldRadius() * intensity * 2);
            effect.SetPosition(position);
            effect.SetRotation(rotation);
            foreach (var mesh in from meshEffect in effect.effects
                                 where meshEffect is EffectMesh
                                 select meshEffect as EffectMesh) {
                Debug.Log(mesh.meshRenderer.material.HasProperty("_Amount"));
                mesh.meshRenderer.material.SetFloat("_Amount", intensity);
            }

        }
        public IEnumerator ThrowEffectCoroutine() {
            var effect = this.effect;
            float startTime = Time.time;
            float duration = 0.5f;
            while (Time.time - startTime < duration) {
                float ratio = (Time.time - startTime) / duration;
                UpdateEffectParams(effect, ratio, Center(), Quaternion.identity);
                yield return 0;
            }
        }
        public void GravityPush() {
            Catalog.GetData<EffectData>("ShatterbladeGravityAoE")
                .Spawn(Center() + ForwardDir() * 0.2f, Quaternion.LookRotation(ForwardDir(), UpDir())).Play();
            Utils.PushForce(Hand().transform.position + ForwardDir() * 1, ForwardDir(), 1, 4, ForwardDir() * 50, true, true);
        }
        public bool HoldingSomething() => targetItem != null;
        public float HeldRadius() => Mathf.Min((targetItem?.GetRadius() + 0.1f) ?? 0.5f, 2f);
        public Vector3 HeldCenter() => targetItem.rb.worldCenterOfMass;

        public Vector3 PincerPos(int i) {
            if (HoldingSomething()) {
                return HeldCenter()
                       + Quaternion.AngleAxis((i == 1 ? 0 : 180) + rotation / 3, HeldCenter() - Center())
                       * UpDir()
                       * (0.1f + HeldRadius());
            } else if (IsButtonPressed()) {
                return Center()
                       + SideDir() * (i == 1 ? 0.1f : -0.1f)
                       + ForwardDir() * 0.15f;
            } else {
                return Center()
                       + UpDir() * (i == 1 ? 1f : -1f) * 0.1f
                       + ForwardDir() * 0.15f;
            }
        }
        public Vector3 InnerRing(int i) {
            if (IsButtonPressed()) {
                return Center()
                       + Quaternion.AngleAxis((i - 2) * (1f / 5f) * 360f + rotation, ForwardDir())
                       * UpDir()
                       * 0.1f;
            } else {
                return Center()
                       + Quaternion.AngleAxis((i - 2) * (1f / 5f) * 360f + rotation, ForwardDir())
                       * UpDir()
                       * (HoldingSomething() ? 0.1f : 0.3f)
                       + ForwardDir() * (HoldingSomething() ? 0.2f : -0.1f);
            }
        }
        public Vector3 OuterRing(int i) {
            if (!HoldingSomething() && IsButtonPressed()) {
                return Center()
                       + Quaternion.AngleAxis((i - 9) * (1f / 7f) * 360f + rotation * -1, ForwardDir())
                       * UpDir() * 0.3f
                       + ForwardDir() * 0.1f;
            } else {
                return Center()
                       + Quaternion.AngleAxis((i - 9) * (1f / 7f) * 360f + rotation * -1, ForwardDir())
                       * UpDir()
                       * (HoldingSomething() ? 0.2f : 0.3f)
                       + ForwardDir() * (HoldingSomething() ? 0.1f : -0.2f);
            }
        }
        public override Vector3 GetPos(int i, Rigidbody rb, BladePart part) {
            if (i < 3) {
                return PincerPos(i);
            } else if (i < 10) {
                return OuterRing(i);
            } else {
                return InnerRing(i);
            }
        }
        public Transform GetTargetTransform() => targetItem?.transform;

        public override Quaternion GetRot(int i, Rigidbody rb, BladePart part) {
            if (i <= 2) {
                if (HoldingSomething()) {
                    return Quaternion.LookRotation(GetTargetTransform().position - rb.transform.position,
                        Center() - rb.transform.position);
                } else if (IsButtonPressed()) {
                    return Quaternion.LookRotation(UpDir(), Center() - ForwardDir() * 0.1f - rb.transform.position);
                }

                return Quaternion.LookRotation(ForwardDir(), SideDir());
            }

            if (HoldingSomething()) {
                return Quaternion.LookRotation(ForwardDir(), rb.transform.position - Center());
            } else if (IsButtonPressed()) {
                return Quaternion.LookRotation(Center() + ForwardDir() * -0.2f - rb.transform.position,
                    Center() + ForwardDir() * 0.3f - rb.transform.position);
            } else {
                return Quaternion.LookRotation(rb.transform.position - Center(), ForwardDir());
            }
        }

        public override void OnButtonPressed() { base.OnButtonPressed(); }

        public override void OnTriggerPressed() {
            effectIntensity = 0;
            effect?.End();
            if (IsButtonPressed() && Time.time - lastTriggerPress > 1f) {
                GravityPush();
            }
        }

        public override void OnTriggerHeld() {
            base.OnTriggerHeld();
            if (!IsButtonPressed() && !HoldingSomething())
                AcquireTarget();
        }

        public override void OnTriggerReleased() {
            base.OnTriggerReleased();
            if (joint)
                Object.Destroy(joint);
            targetItem?.collisionHandlers.ForEach(ch => ch.RemovePhysicModifier(this));
            if (HoldingSomething())
                ThrowHeldThing();
            effect?.End();
            targetItem = null;
            joint = null;
        }

        public override string GetUseAnnotation() => IsButtonPressed()
            ? "Pull trigger for gravity blast"
            : (HoldingSomething() ? "Release trigger to throw" : "Pull trigger to attract an object");
        public override bool GetUseAnnotationShown() => true;
        public override string GetAltUseAnnotation() => "Hold button to switch modes";
        public override bool GetAltUseAnnotationShown() => !IsButtonPressed() && !IsTriggerPressed();
        public override float Cooldown() => IsButtonPressed() ? 1f : 0;
        public override void Update() {
            base.Update();
            targetPoint.transform.position = Center() + ForwardDir() * (0.2f + HeldRadius() * 1.5f);
            targetPoint.transform.rotation = Quaternion.LookRotation(ForwardDir(), UpDir());
            if (HoldingSomething()) {
                targetItem.rb.velocity *= Mathf.Lerp(0.8f, 1, Mathf.InverseLerp(0, 4, Vector3.Distance(targetItem.transform.position, targetPoint.position)));
                targetItem.rb.velocity = targetItem.rb.velocity.magnitude
                                         * (targetPoint.transform.position - targetItem.transform.position).normalized;
                if (effect != null) {
                    effectIntensity = Mathf.Min(effectIntensity + Time.deltaTime * 3f, 1);
                    UpdateEffectParams(effect, effectIntensity, HeldCenter(), targetItem.transform.rotation);
                }
                rotation += Time.deltaTime * Mathf.Lerp(80, 300, Mathf.Clamp01((Time.time - lastTriggerPress) * (1f / 1f)));
            } else {
                rotation += Time.deltaTime * 80;
            }
        }

        public override void Exit() {
            base.Exit();
            Object.Destroy(joint);
            Object.Destroy(targetPoint);
            effect?.End();
            targetItem?.collisionHandlers.ForEach(ch => ch.RemovePhysicModifier(this));
        }
    }
}
