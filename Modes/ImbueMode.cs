using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade {
    abstract class ImbueMode : BladeMode {
        public List<Rigidbody> jointParts;
        private bool wasButtonPressed;
        private bool wasTriggerPressed;
        protected float lastButtonPress;
        protected float lastTriggerPress;
        protected float lastButtonReleased;
        protected float lastTriggerReleased;
        private Annotation useAnnotation;
        private Annotation altUseAnnotation;
        public virtual RagdollHand Hand() => GetPart().item.mainHandler;
        public BladePart GetPart() => sword.GetPart(11);
        public virtual Vector3 Center() => Hand().transform.position + Hand().PointDir() * 0.2f;
        public virtual Vector3 UpDir() => Hand().ThumbDir();
        public virtual Vector3 ForwardDir() => Hand().PointDir();
        public virtual Vector3 SideDir() => Hand().PalmDir();
        public abstract Vector3 GetPos(int index, Rigidbody rb, BladePart part);
        public abstract Quaternion GetRot(int index, Rigidbody rb, BladePart part);
        public Vector3 GetUseAnnotationPosition()
            => Hand().side == Side.Left ? new Vector3(1, -1, 1) : new Vector3(1, -1, -1);
        public Vector3 GetAltUseAnnotationPosition()
            => Hand().side == Side.Left ? new Vector3(1, 1, 1) : new Vector3(1, 1, -1);

        public virtual float Cooldown() => 0;

        public virtual bool GetUseAnnotationShown() => false;
        public virtual bool GetAltUseAnnotationShown() => false;
        public virtual string GetUseAnnotation() => "";
        public virtual string GetAltUseAnnotation() => "";
        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            jointParts = new List<Rigidbody>();
            sword.shouldLock = false;
            sword.animator.enabled = false;
            sword.jointRBs.ForEach(rb => rb.transform.parent = null);
            for (int i = 1; i < 16; i++) {
                if (i != 11)
                    jointParts.Add(sword.GetRB(i));
            }
            foreach (var hand in GetPart().item.handlers)
                sword.IgnoreCollider(hand, true);
            sword.ReformParts();
            GetPart().Detach();
            useAnnotation = Annotation.CreateAnnotation(sword, GetPart().transform,
                GetPart().transform, GetUseAnnotationPosition());
            altUseAnnotation = Annotation.CreateAnnotation(sword, GetPart().transform,
                sword.GetPart(11).transform, GetAltUseAnnotationPosition());
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
        public bool IsButtonPressed() => Hand().playerHand.controlHand.alternateUsePressed;
        public bool IsTriggerPressed() => Hand().playerHand.controlHand.usePressed && Time.time - lastTriggerReleased > Cooldown();

        public virtual void OnButtonPressed() => lastButtonPress = Time.time;
        public virtual void OnButtonHeld() {}
        public virtual void OnButtonNotHeld() {}

        public virtual void OnButtonReleased() => lastButtonReleased = Time.time;

        public virtual void OnTriggerPressed() => lastTriggerPress = Time.time;
        public virtual void OnTriggerHeld() {}
        public virtual void OnTriggerNotHeld() {}

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

        public IEnumerable<Rigidbody> PartsInOrder() => from part in sword.jointRBs
            where part.name != "Blade_11"
            orderby int.Parse(part.name.Split('_')[1])
            select part;
        public override void Update() {
            base.Update();
            CheckInputs();
            int i = 1;
            useAnnotation.offset = GetUseAnnotationPosition();
            altUseAnnotation.offset = GetAltUseAnnotationPosition();
            if (GetUseAnnotationShown()) {
                useAnnotation.SetText(GetUseAnnotation());
            } else {
                useAnnotation.Hide();
            }
            if (GetAltUseAnnotationShown()) {
                altUseAnnotation.SetText(GetAltUseAnnotation());
            } else {
                altUseAnnotation.Hide();
            }
            foreach (var jointPart in PartsInOrder()) {
                var part = sword.rbMap[jointPart];
                jointPart.transform.position = GetPos(i, jointPart, part);
                jointPart.transform.rotation = GetRot(i, jointPart, part)
                                               * Quaternion.Inverse(part.item
                                                   .GetFlyDirRefLocalRotation());
                i++;
            }
        }
        public override bool ShouldReform(BladePart part) => part != sword.GetPart(11);
    }
}
