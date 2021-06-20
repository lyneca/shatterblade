using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;
using System.Collections;
using Shatterblade.Modes;
using static UnityEngine.Object;

namespace Shatterblade {
    public class ShatterbladeModule : ItemModule {
        public bool tutorial;
        public override void OnItemLoaded(Item item) {
            base.OnItemLoaded(item);
            if (Level.current.data.id == "CharacterSelection")
                return;
            var shatterblade = item.gameObject.AddComponent<Shatterblade>();
            shatterblade.isTutorialBlade = tutorial;
        }
    }
    public class Shatterblade : MonoBehaviour {
        const float BUTTON_TAP_THRESHOLD = 0.3f;
        public bool isTutorialBlade;
        public bool locking;
        public bool wasLocking;
        public List<BladePart> parts = new List<BladePart>();
        bool buttonWasPressed;
        private bool hasFlashed;
        bool isReady;
        public List<Rigidbody> jointRBs = new List<Rigidbody>();
        float lastButtonPress;
        public Item item;
        float lastBladeHaptic;
        public BladeMode mode;
        public Animator animator;
        public RagdollHand buttonHand;
        public Dictionary<Rigidbody, BladePart> rbMap = new Dictionary<Rigidbody, BladePart>();
        public Dictionary<BladePart, Rigidbody> partMap = new Dictionary<BladePart, Rigidbody>();
        public bool shouldLock = true;
        public Dictionary<int, bool> isSpawning = new Dictionary<int, bool>();
        public bool isDespawned;
        public Annotation handleAnnotationA;
        public Annotation handleAnnotationB;
        public Annotation imbueShardAnnotation;
        public Annotation gunShardAnnotation;
        public Annotation imbueHandleAnnotation;

        public void Start() {
            Init();
        }
        public void Init() {
            if (!gameObject || Player.currentCreature?.handLeft == null)
                return;
            item = GetComponent<Item>();
            parts = new List<BladePart>();
            item.ResetCenterOfMass();
            animator = GetComponentInChildren<Animator>();
            item.OnDespawnEvent += OnDespawnEvent;
            ListenForHand(Player.currentCreature.handRight);
            ListenForHand(Player.currentCreature.handLeft);
            wasLocking = true;

            handleAnnotationA = Annotation.CreateAnnotation(this, item.mainHandleLeft.transform, transform, new Vector3(0, 1, -1));
            handleAnnotationB = Annotation.CreateAnnotation(this, item.mainHandleLeft.transform, transform, new Vector3(0, -1, 1));
            handleAnnotationA.Hide();
            handleAnnotationB.Hide();

            jointRBs = animator.GetComponentsInChildren<Rigidbody>().ToList();
            for (int i = 0; i < 15; i++) {
                isSpawning[i + 1] = true;
            }
            if (item.handlers.Any()) {
                SpawnAllParts();
            } else {
                item.OnGrabEvent += PickUpEvent;
            }
            ChangeMode<SwordMode>();
        }
        public void GrabEvent(Side side, Handle handle, float axisPosition, HandleOrientation orientation, EventTime time) => IgnoreCollider(Player.currentCreature.GetHand(side), true);
        public void UnGrabEvent(Side side, Handle handle, bool throwing, EventTime time) => IgnoreCollider(Player.currentCreature.GetHand(side), false);
        public void ListenForHand(RagdollHand hand) {
            hand.OnGrabEvent += GrabEvent;
            hand.OnUnGrabEvent += UnGrabEvent;
        }
        public void StopListening(RagdollHand hand) {
            hand.OnGrabEvent -= GrabEvent;
            hand.OnUnGrabEvent -= UnGrabEvent;
        }
        public void OnDespawnEvent() {
            isDespawned = true;
            mode?.Exit();
            mode = null;
            StopListening(Player.currentCreature.handRight);
            StopListening(Player.currentCreature.handLeft);
            DetachParts(false);
            handleAnnotationA?.Destroy();
            handleAnnotationB?.Destroy();
            imbueShardAnnotation?.Destroy();
            imbueHandleAnnotation?.Destroy();
            gunShardAnnotation?.Destroy();
        }
        public void SpawnAllParts() {
            for (int i = 0; i < 15; i++) {
                SpawnPart(i + 1);
            }
        }
        public void PickUpEvent(Handle handle, RagdollHand hand) {
            SpawnAllParts();
            item.OnGrabEvent -= PickUpEvent;
        }
        public void ShowAll() {
            foreach (var part in parts) {
                part.Show();
            }
        }
        public void SpawnPart(int i) {
            isSpawning[i] = true;
            Catalog.GetData<ItemData>($"ShatterbladePart{i}").SpawnAsync(item => {
                var targetRB = jointRBs.FirstOrDefault(obj => obj.name == $"Blade_{i}").gameObject.GetComponent<Rigidbody>();
                item.transform.position = targetRB.transform.position;
                item.transform.rotation = targetRB.transform.rotation;
                var part = item.gameObject.AddComponent<BladePart>();
                parts.Add(part);
                part.Init(this, targetRB);
                rbMap[targetRB] = part;
                partMap[part] = targetRB;
                isSpawning[i] = false;
                if (i == 1) {
                    if (imbueShardAnnotation) {
                        Destroy(imbueShardAnnotation);
                        imbueShardAnnotation = null;
                    }
                    imbueShardAnnotation = Annotation.CreateAnnotation(this, item.transform, this.item.transform, new Vector3(1, -1, 0));
                } else if (i == 10) {
                    if (gunShardAnnotation) {
                        Destroy(gunShardAnnotation);
                        gunShardAnnotation = null;
                    }
                    gunShardAnnotation = Annotation.CreateAnnotation(this, item.transform, this.item.transform, new Vector3(1, -2, 0));
                } else if (i == 11) {
                    if (imbueHandleAnnotation) {
                        Destroy(imbueHandleAnnotation);
                        imbueHandleAnnotation = null;
                    }
                    imbueHandleAnnotation = Annotation.CreateAnnotation(this, item.transform, this.item.transform, new Vector3(-1, -2, 0));
                }
            });
        }

        public void HideAllAnnotations() {
            imbueHandleAnnotation.Hide();
            imbueShardAnnotation.Hide();
            gunShardAnnotation.Hide();
            handleAnnotationA.Hide();
            handleAnnotationB.Hide();
        }
        public void IgnoreOtherBladeParts(BladePart part) {
            foreach (BladePart otherPart in parts) {
                if (part != otherPart) {
                    foreach (var collider in part.item.colliderGroups.First().colliders) {
                        foreach (var otherCollider in otherPart.item.colliderGroups.First().colliders) {
                            Physics.IgnoreCollision(collider, otherCollider);
                        }
                    }
                }
            }
            foreach (var collider in part.item.colliderGroups.First().colliders) {
                foreach (var otherCollider in item.GetComponentsInChildren<Collider>()) {
                    Physics.IgnoreCollision(collider, otherCollider);
                }
            }
        }
        public void PostInit() {
            isReady = true;
            foreach (BladePart part in parts) {
                IgnoreOtherBladeParts(part);
            }
            foreach (var handler in item.handlers) {
                IgnoreCollider(handler, true);
            }
            locking = true;
            ReformParts();
        }
        public void IgnoreRagdoll(Ragdoll ragdoll, bool ignore) {
            foreach (var part in parts) {
                part.IgnoreRagdoll(ragdoll, ignore);
            }
        }
        public void IgnoreCollider(RagdollHand hand, bool ignore) {
            foreach (var part in parts) {
                part.IgnoreHand(hand, ignore);
            }
        }
        public void FixedUpdate() {
            if (!isReady)
                return;
            if (item.handlers.Any(handler => handler.playerHand.controlHand.alternateUsePressed)) {
                foreach (var handler in item.handlers) {
                    item.mainHandleLeft.SetSliding(handler, false);
                    item.mainHandleLeft.StartCoroutine(StopSliding());
                }
            }
        }
        public IEnumerator StopSliding() {
            yield return new WaitForEndOfFrame();
            if (item.handlers.Any(handler => handler.playerHand.controlHand.alternateUsePressed)) {
                foreach (var handler in item.handlers) {
                    item.mainHandleLeft.SetSliding(handler, false);
                }
            }
        }

        public void ChangeMode<T>() where T : BladeMode, new() {
            if (mode is T) return;
            mode?.Exit();
            //Debug.Log($"changing to state {typeof(T)}");
            foreach (var hand in item.handlers)
                IgnoreCollider(hand, true);
            mode = new T();
            mode.Enter(this);
        }

        public void CheckForMissingParts() {
            parts = parts.NotNull().Where(part => part?.gameObject?.transform != null).ToList();
            for (int i = 0; i < 15; i++) {
                var partNames = parts.Select(part => part?.item?.itemId);
                if (!partNames.Contains($"ShatterbladePart{i + 1}") && !isSpawning[i + 1]) {
                    SpawnPart(i + 1);
                    isReady = false;
                }
            }
        }

        public bool TopShardIsImbued() => GetPart(1).item.imbues.FirstOrDefault() is Imbue imbue
                                          && imbue.energy / imbue.maxEnergy > 0.3f
                                          && (imbue.spellCastBase is SpellCastGravity
                                              || imbue.spellCastBase is SpellCastLightning
                                              || imbue.spellCastBase is SpellCastProjectile);

        public void Update() {
            if (item == null) {
                Init();
                if (item == null)
                    return;
            }
            CheckForMissingParts();
            if (!isReady && parts.Count() == 15) {
                PostInit();
            } else if (!isReady) {
                return;
            }

            var lockedParts = from part in parts
                              where part.state == PartState.Locked
                              select part;
            if (false && lockedParts.Any()) {
                float minDistance = lockedParts.Min(part => part.DistanceToClosestHand());
                float maxDistance = lockedParts.Max(part => part.DistanceToClosestHand());
                if (lockedParts.Count() == 15) {
                    if (!hasFlashed) {
                        hasFlashed = true;
                        foreach (var part in from part in lockedParts
                                             orderby part.DistanceToClosestHand()
                                             select part) {
                            this.RunAfter(() => part.Flash(),
                                (part.DistanceToClosestHand() - minDistance) / (maxDistance - minDistance) * 0.2f);
                        }
                    }
                } else {
                    hasFlashed = false;
                }
            }

            if (GetPart(11)?.item?.mainHandler != null
                && GetPart(1).item.imbues.FirstOrDefault() is Imbue imbue
                && TopShardIsImbued()) {
                if (imbue.spellCastBase is SpellCastGravity) {
                    ChangeMode<GravityMode>();
                } else if (imbue.spellCastBase is SpellCastLightning) {
                    ChangeMode<LightningMode>();
                } else if (imbue.spellCastBase is SpellCastProjectile) {
                    ChangeMode<FlamethrowerMode>();
                }
            } else if (GetPart(10)?.item?.mainHandler != null) {
                ChangeMode<CannonMode>();
            } else if (item.handlers.Any(handler => handler.playerHand.controlHand.alternateUsePressed)) {
                if (!buttonWasPressed) {
                    buttonWasPressed = true;
                    lastButtonPress = Time.time;
                }
                locking = true;
                // Trigger behaviour
                if (item.handlers.Any(handler => handler.playerHand.controlHand.usePressed)) {
                    if (item.handlers.Where(hand => hand.playerHand.controlHand.usePressed).Count() > 1) {
                        buttonHand = item.mainHandler;
                    } else {
                        buttonHand = item.handlers.Where(hand => hand.playerHand.controlHand.usePressed).FirstOrDefault();
                    }
                    ChangeMode<ShieldMode>();
                } else {
                    ChangeMode<ExpandedMode>();
                }
            } else {
                ChangeMode<SwordMode>();
                // Drop parts on button tap
                if (buttonWasPressed) {
                    if (Time.time - lastButtonPress < BUTTON_TAP_THRESHOLD) {
                        locking = !wasLocking;
                        if (locking) {
                            ReformParts();
                        } else {
                            DetachParts();
                        }
                        wasLocking = locking;
                    } else {
                        wasLocking = true;
                    }
                }
                buttonWasPressed = false;
            }
            mode?.Update();
        }
        public void BladeHaptic(float velocity) {
            if (mode is CannonMode)
                return;
            if (Time.time - lastBladeHaptic > 0.01f) {
                lastBladeHaptic = Time.time;
                item.handlers.ForEach(handler => handler.HapticTick(Mathf.InverseLerp(0, 10, velocity) * 0.5f));
            }
        }
        public void DetachParts(bool shouldThrow = true) {
            foreach (var part in parts) {
                part.Detach(shouldThrow);
            }
        }
        public void ReformParts() {
            if (!isReady) return;
            locking = true;
            foreach (var part in parts) {
                if (mode?.ShouldReform(part) ?? true)
                    part.Reform();
            }
        }
        public bool ShouldReform(BladePart part) => locking && (mode?.ShouldReform(part) ?? true);
        public BladePart GetPart(int index) => parts.FirstOrDefault(part => part.item.itemId == $"ShatterbladePart{index}");
        public Rigidbody GetRB(int index) => jointRBs.FirstOrDefault(rb => rb.name == $"Blade_{index}");
    }
    public abstract class BladeMode {
        public Shatterblade sword;
        public virtual void Enter(Shatterblade sword) => this.sword = sword;
        public virtual void Update() { }
        public virtual void Exit() { }
        public virtual bool ShouldReform(BladePart part) => true;
    }
}
