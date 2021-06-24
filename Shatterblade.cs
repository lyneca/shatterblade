using System;
using System.CodeDom;
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
        public bool tutorial = false;
        public bool discoMode = false;
        public float damageModifier = 1;
        public float jointMaxForce = 100;
        public override void OnItemLoaded(Item item) {
            base.OnItemLoaded(item);
            if (Level.current.data.id == "CharacterSelection")
                return;
            var shatterblade = item.gameObject.AddComponent<Shatterblade>();
            shatterblade.isTutorialBlade = tutorial;
            shatterblade.module = this;
        }
    }
    public class Shatterblade : MonoBehaviour {
        public ShatterbladeModule module;
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
        public Annotation otherHandAnnotation;
        public Annotation gunShardAnnotation;
        public Annotation sawShardAnnotation;
        public Annotation swarmShardAnnotation;
        public Annotation imbueHandleAnnotation;
        private float orgAxisLength;
        public List<BladeMode> modes;
        public void Awake() {
            modes = new List<BladeMode>() { };
        }

        public void Start() {
            modes = modes.OrderBy(mode => mode.Priority()).ToList();
            Init();
        }
        public void RegisterMode(BladeMode newMode) {
            modes.Add(newMode);
        }
        public void Init() {
            if (!gameObject || Player.currentCreature?.handLeft == null)
                return;
            item = GetComponent<Item>();
            orgAxisLength = item.mainHandleLeft.axisLength;
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
        public void UnGrabEvent(Side side, Handle handle, bool throwing, EventTime time) => IgnoreCollider(Player.currentCreature.GetHand(side), false, 0.5f);
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
            otherHandAnnotation?.Destroy();
            imbueHandleAnnotation?.Destroy();
            gunShardAnnotation?.Destroy();
            sawShardAnnotation?.Destroy();
            swarmShardAnnotation?.Destroy();
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
                    if (otherHandAnnotation) {
                        Destroy(otherHandAnnotation);
                        otherHandAnnotation = null;
                    }
                    otherHandAnnotation = Annotation.CreateAnnotation(this, item.transform, this.item.transform, new Vector3(1, -1, 0));
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
                } else if (i == 12) {
                    if (sawShardAnnotation) {
                        Destroy(sawShardAnnotation);
                        sawShardAnnotation = null;
                    }
                    sawShardAnnotation = Annotation.CreateAnnotation(this, item.transform, this.item.transform, new Vector3(-1, 0.5f, 0));
                } else if (i == 13) {
                    if (swarmShardAnnotation) {
                        Destroy(swarmShardAnnotation);
                        swarmShardAnnotation = null;
                    }
                    swarmShardAnnotation = Annotation.CreateAnnotation(this, item.transform, this.item.transform, new Vector3(0, 2, 0));
                }
            });
        }

        public void HideAllAnnotations() {
            imbueHandleAnnotation.Hide();
            otherHandAnnotation.Hide();
            gunShardAnnotation.Hide();
            sawShardAnnotation.Hide();
            swarmShardAnnotation.Hide();
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
        public void IgnoreCollider(RagdollHand hand, bool ignore, float delay = 0) {
            foreach (var part in parts) {
                part.IgnoreHand(hand, ignore, delay);
            }
        }

        public void ChangeMode<T>() where T : BladeMode, new() {
            if (mode is T) return;
            mode?.Exit();
            foreach (var hand in item.handlers)
                IgnoreCollider(hand, true);
            mode = new T();
            mode.Enter(this);
        }

        public void ChangeMode(BladeMode newMode) {
            var newModeInstance = newMode.Clone();
            if (mode.GetType() == newModeInstance.GetType()) {
                return;
            }
            mode?.Exit();
            foreach (var hand in item.handlers)
                IgnoreCollider(hand, true);
            mode = newModeInstance;
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

            if (item.holder == null) {
                HideAllAnnotations();
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

            bool changedModes = false;
            foreach (var mode in modes) {
                if (mode.Test(this)) {
                    ChangeMode(mode);
                    changedModes = true;
                }
            }

            if (!changedModes) {
                if (item.handlers.Any(handler => handler.playerHand.controlHand.alternateUsePressed)) {
                    if (!buttonWasPressed) {
                        buttonWasPressed = true;
                        lastButtonPress = Time.time;
                        orgAxisLength = item.mainHandleLeft.axisLength;
                        item.mainHandleLeft.axisLength = 0;
                        foreach (var handler in item.handlers) {
                            item.mainHandleLeft.SetSliding(handler, false);
                        }
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
                        item.mainHandleLeft.axisLength = orgAxisLength;
                    }
                    buttonWasPressed = false;
                }
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

        public bool ShouldHide(BladePart part) => mode?.ShouldHideWhenHolstered(part) ?? true;
        public bool ShouldReform(BladePart part) => locking && (mode?.ShouldReform(part) ?? true);
        public bool ShouldPartLock(BladePart part) => locking && (mode?.ShouldLock(part) ?? true);
        public void ModifyJoint(BladePart part) => mode?.JointModifier(part.joint, part);
        public BladePart GetPart(int index) => parts.FirstOrDefault(part => part.item.itemId == $"ShatterbladePart{index}");
        public Rigidbody GetRB(int index) => jointRBs.FirstOrDefault(rb => rb.name == $"Blade_{index}");
    }

    /// <summary>
    /// Base class for every mode of the Shatterblade.
    /// If you're using this: good luck. I would recommend
    /// decompiling the source, or DM'ing me - I'd be happy to help!
    /// </summary>
    public abstract class BladeMode : ItemModule {

        public BladeMode Clone() {
            return (BladeMode)MemberwiseClone();
        }

        /// <summary>
        /// Called when the item is first spawned. You likely don't want to touch this.
        /// </summary>
        /// <param name="item"></param>
        public override void OnItemLoaded(Item item) {
            base.OnItemLoaded(item);
            if (item.GetComponent<Shatterblade>() is Shatterblade shatterblade) {
                shatterblade.RegisterMode(this);
            }
        }

        /// <summary>
        /// Determines the priority of the ability. The higher the number, the sooner it's checked in the list.
        /// E.g. if you want to override the Lightning ability, set this to a number higher than it.
        /// All base abilities default to a priority less than zero.
        /// </summary>
        /// <returns>The priority of the mode</returns>
        public virtual float Priority() => -1;

        /// <summary>
        /// The sword this mode is running on.
        /// </summary>
        public Shatterblade sword;

        /// <returns>This should return true if the mode should be entered, and should only return false when it should be exited.</returns>
        /// <param name="sword">The Shatterblade to which this mode is attached.</param>
        public abstract bool Test(Shatterblade sword);

        /// <summary>
        /// Called when the mode is first entered.
        /// </summary>
        /// <param name="sword">The Shatterblade to which this mode is attached. This is passed into BladeMode.sword.</param>
        public virtual void Enter(Shatterblade sword) => this.sword = sword;

        /// <summary>
        /// Called once per frame while the mode is active.
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// Called once when the state is exited.
        /// </summary>
        public virtual void Exit() { }

        /// <summary>
        /// Given a part, defines whether the part should attempt to navigate to its target position.
        ///
        /// If your ability throws a part, make sure that this returns false while the part is in the air!
        /// </summary>
        public virtual bool ShouldReform(BladePart part) => true;

        /// <summary>
        /// Given a part, defines whether the part should attempt to hard lock to the part. You likely won't need this.
        /// </summary>
        public virtual bool ShouldLock(BladePart part) => true;

        /// <summary>
        /// Given a part, defines whether that part should vanish when the sword is holstered in this mode. You likely won't need this.
        /// </summary>
        public virtual bool ShouldHideWhenHolstered(BladePart part) => false;

        /// <summary>
        /// This function is called after a blade shard creates or updates its joint.
        /// This is a powerful tool for customizing how the shards move, but you likely won't need to use it.
        /// </summary>
        /// <param name="joint">The joint that was created or updated.</param>
        /// <param name="part">The part that owns the joint in question.</param>
        public virtual void JointModifier(ConfigurableJoint joint, BladePart part) { }
    }
}
