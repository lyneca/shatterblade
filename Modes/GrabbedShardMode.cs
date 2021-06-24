using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade.Modes {
    public abstract class GrabbedShardMode : BladeMode {
        public List<Rigidbody> jointParts;
        private string lastUseText = "";
        private string lastAltUseText = "";
        private bool wasButtonPressed;
        private bool wasTriggerPressed;

        /// <summary>The time at which the button was last pressed.</summary>
        protected float lastButtonPress;

        /// <summary>The time at which the trigger was last pressed.</summary>
        protected float lastTriggerPress;

        /// <summary>The time at which the button was last released.</summary>
        protected float lastButtonReleased;

        /// <summary>The time at which the trigger was last released.</summary>
        protected float lastTriggerReleased;
        private Annotation useAnnotation;
        private Annotation altUseAnnotation;

        /// <param name="sword">The Shatterblade to which this mode is attached.</param>
        /// <returns>True if the target shard is grabbed. Override to add more conditions.</returns>
        public override bool Test(Shatterblade sword) => sword.GetPart(TargetPartNum())?.item?.mainHandler != null;

        /// <summary>
        /// This method should return the shard number (from 1-15) that, upon being grabbed, will trigger the ability.
        /// </summary>
        public abstract int TargetPartNum();

        /// <returns>The hand holding the trigger shard.</returns>
        public virtual RagdollHand Hand() => GetPart().item.mainHandler;

        /// <returns>The part that triggers the ability.</returns>
        public BladePart GetPart() => sword.GetPart(TargetPartNum());

        /// <returns>By default, a point in space 0.2m in front of the player's hand (in the direction they are pointing).</returns>
        public virtual Vector3 Center() => Hand().transform.position + Hand().PointDir() * 0.2f;

        /// <returns>Direction up from the player's hand (in the direction of their thumb)</returns>
        public virtual Vector3 UpDir() => Hand().ThumbDir();

        /// <returns>Direction forwards from the player's hand (as if they were pointing)</returns>
        public virtual Vector3 ForwardDir() => Hand().PointDir();

        /// <returns>Direction to the side from the player's hand (in the direction of their palm)</returns>
        public virtual Vector3 SideDir() => Hand().PalmDir();

        /// <summary>
        /// Override this method to tell each shard where it should attempt to move, given its index.
        /// </summary>
        /// <param name="index">Index of the shard (1-14, as it does not include the held shard)</param>
        /// <param name="rb">Target rigidbody of the shard (where the shard currently wants to go)</param>
        /// <param name="part">The BladePart attached to the shard</param>
        /// <returns>The position in world space to which the shard should fly.</returns>
        public abstract Vector3 GetPos(int index, Rigidbody rb, BladePart part);

        /// <summary>
        /// Override this method to tell each shard where it should attempt to rotate, given its index.
        ///
        /// Note that this rotation is aligned to the shard's FlyDirRef. This means that, generally,
        /// if you return this:
        /// <code>Quaternion.LookRotation(ForwardDir(), UpDir())</code>
        /// The sharp side of the shard will point forward and the flat side will point upwards.
        /// </summary>
        /// <param name="index">Index of the shard (1-14, as it does not include the held shard)</param>
        /// <param name="rb">Target rigidbody of the shard (where the shard currently wants to go)</param>
        /// <param name="part">The BladePart attached to the shard</param>
        /// <returns>Quaternion indicating how the shard should orient itself.</returns>
        public abstract Quaternion GetRot(int index, Rigidbody rb, BladePart part);

        /// <summary>This method determines where the tutorial text should render on the tutorial version of the blade. You can likely leave it at default, unless it clips with whatever your mode does.</summary>
        /// <returns>The position the trigger annotation should render at. Defaults to a position near the palm side of your hand.</returns>
        public Vector3 GetUseAnnotationPosition()
            => Hand().side == Side.Left ? new Vector3(1, -1, 1.5f) : new Vector3(1, -1, -1.5f);

        /// <summary>This method determines where the tutorial text should render on the tutorial version of the blade. You can likely leave it at default, unless it clips with whatever your mode does.</summary>
        /// <returns>The position the button annotation should render at. Defaults to a position near the palm side of your hand.</returns>
        public Vector3 GetAltUseAnnotationPosition()
            => Hand().side == Side.Left ? new Vector3(1, 1, 1.5f) : new Vector3(1, 1, -1.5f);

        /// <summary>
        /// Defines a cooldown between trigger presses, allowing you to prevent the player from spamming your ability. Defaults to no cooldown.
        /// </summary>
        /// <returns>Cooldown between trigger presses.</returns>
        public virtual float Cooldown() => 0;

        /// <summary>Override this method to conditionally show or hide the trigger annotation.</summary>
        /// <returns>True if the trigger annotation should currently be shown.</returns>
        public virtual bool GetUseAnnotationShown() => false;

        /// <summary>Override this method to conditionally show or hide the button annotation.</summary>
        /// <returns>True if the button annotation should currently be shown.</returns>
        public virtual bool GetAltUseAnnotationShown() => false;

        /// <summary>Override this method to determine the text the trigger annotation shows. Updates live as you change it.</summary>
        /// <returns>Tutorial annotation for the trigger.</returns>
        public virtual string GetUseAnnotation() => "";

        /// <summary>Override this method to determine the text the button annotation shows. Updates live as you change it.</summary>
        /// <returns>Tutorial annotation for the button.</returns>
        public virtual string GetAltUseAnnotation() => "";

        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            jointParts = new List<Rigidbody>();
            sword.shouldLock = false;
            sword.animator.enabled = false;
            sword.jointRBs.ForEach(rb => rb.transform.parent = null);
            for (int i = 1; i < 16; i++) {
                if (i != TargetPartNum())
                    jointParts.Add(sword.GetRB(i));
            }
            foreach (var hand in GetPart().item.handlers)
                sword.IgnoreCollider(hand, true);
            sword.ReformParts();
            GetPart().Detach();
            useAnnotation = Annotation.CreateAnnotation(sword, GetPart().transform,
                GetPart().transform, GetUseAnnotationPosition());
            altUseAnnotation = Annotation.CreateAnnotation(sword, GetPart().transform,
                sword.GetPart(TargetPartNum()).transform, GetAltUseAnnotationPosition());
            sword.HideAllAnnotations();
        }

        public override void Exit() {
            base.Exit(); 
            useAnnotation.Destroy();
            altUseAnnotation.Destroy();
            sword.jointRBs.ForEach(rb => rb.transform.parent = sword.animator.transform);
            sword.animator.enabled = true;
            sword.shouldLock = true;
        }

        /// <returns>True if the player is pressing the alt use button on their controller.</returns>
        public bool IsButtonPressed() => Hand().playerHand.controlHand.alternateUsePressed;

        /// <returns>True if the player is pressing the trigger on their controller.</returns>
        public bool IsTriggerPressed() => Hand().playerHand.controlHand.usePressed && Time.time - lastTriggerReleased > Cooldown();

        /// <summary>
        /// Called when the button is pressed.
        /// </summary>
        public virtual void OnButtonPressed() => lastButtonPress = Time.time;

        /// <summary>
        /// Called once per frame while the button is held.
        /// </summary>
        public virtual void OnButtonHeld() {}

        /// <summary>
        /// Called once per frame while the button is not held.
        /// </summary>
        public virtual void OnButtonNotHeld() {}

        /// <summary>
        /// Called when the button is released.
        /// </summary>
        public virtual void OnButtonReleased() => lastButtonReleased = Time.time;

        /// <summary>
        /// Called when the trigger is pressed.
        /// </summary>
        public virtual void OnTriggerPressed() => lastTriggerPress = Time.time;

        /// <summary>
        /// Called once per frame while the trigger is held.
        /// </summary>
        public virtual void OnTriggerHeld() {}

        /// <summary>
        /// Called once per frame while the trigger is not held.
        /// </summary>
        public virtual void OnTriggerNotHeld() {}

        /// <summary>
        /// Called when the trigger is released.
        /// </summary>
        public virtual void OnTriggerReleased() => lastTriggerReleased = Time.time;

        private void CheckInputs() {
            if (IsTriggerPressed()) {
                if (!wasTriggerPressed) {
                    wasTriggerPressed = true;
                    OnTriggerPressed();
                }
                OnTriggerHeld();
            } else {
                if (wasTriggerPressed) {
                    wasTriggerPressed = false;
                    OnTriggerReleased();
                }
                OnTriggerNotHeld();
            }
            if (IsButtonPressed()) {
                if (!wasButtonPressed) {
                    wasButtonPressed = true;
                    OnButtonPressed();
                }
                OnButtonHeld();
            } else {
                if (wasButtonPressed) {
                    wasButtonPressed = false;
                    OnButtonReleased();
                }
                OnButtonNotHeld();
            }
        }

        public override void Update() {
            base.Update();
            CheckInputs();
            useAnnotation.offset = GetUseAnnotationPosition();
            altUseAnnotation.offset = GetAltUseAnnotationPosition();
            if (GetUseAnnotationShown()) {
                if (lastUseText != GetUseAnnotation()) {
                    useAnnotation.SetText(GetUseAnnotation());
                    lastUseText = GetUseAnnotation();
                }
            } else {
                useAnnotation.Hide();
                lastUseText = "";
            }
            if (GetAltUseAnnotationShown()) {
                if (lastAltUseText != GetAltUseAnnotation()) {
                    altUseAnnotation.SetText(GetAltUseAnnotation());
                    lastAltUseText = GetAltUseAnnotation();
                }
            } else {
                altUseAnnotation.Hide();
                lastAltUseText = "";
            }

            int i = 1;
            foreach (Rigidbody jointPart in jointParts) {
                var part = sword.rbMap[jointPart];
                jointPart.transform.position = GetPos(i, jointPart, part);
                jointPart.transform.rotation = GetRot(i, jointPart, part)
                                               * Quaternion.Inverse(part.item
                                                   .GetFlyDirRefLocalRotation());
                i++;
            }
        }
        /// <summary>
        /// Given a part, defines whether the part should attempt to navigate to its target position.
        ///
        /// If your ability throws a part, make sure that this returns false while the part is in the air!
        /// </summary>
        public override bool ShouldReform(BladePart part) => part != sword.GetPart(TargetPartNum());

        /// <summary>
        /// Given a part, defines whether the part should attempt to hard lock to the part. You likely won't need this.
        /// </summary>
        public override bool ShouldLock(BladePart part) => part != sword.GetPart(TargetPartNum());
    }
}
