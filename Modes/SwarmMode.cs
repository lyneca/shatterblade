using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade.Modes {
    class SwarmMode : GrabbedShardMode {
        private Dictionary<int, Vector3> offsets;
        private Dictionary<int, Vector3> axes;
        private float rotation;

        public override Vector3 Center() => base.Center() + ForwardDir() * 1.5f;

        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            offsets = new Dictionary<int, Vector3>();
            axes = new Dictionary<int, Vector3>();
            for (int i = 1; i < 16; i++) {
                offsets[i] = Utils.RandomVector(-1, 1);
                axes[i] = Utils.RandomVector(-1, 1);
            }
        }
        public override int TargetPartNum() => 13;

        public override Vector3 GetPos(int index, Rigidbody rb, BladePart part) => Center()
            + Quaternion.AngleAxis(rotation, axes[index])
            * offsets[index]
            * (IsButtonPressed() ? 0.2f : 1f);

        public override Quaternion GetRot(int index, Rigidbody rb, BladePart part)
            => Quaternion.LookRotation(Center() - rb.transform.position, Vector3.up);
        public override bool ShouldLock(BladePart part) => false;

        public override void JointModifier(ConfigurableJoint joint, BladePart part) {
            var posDrive = joint.xDrive;
            posDrive.positionSpring = 80;
            posDrive.positionDamper = 20f;
            posDrive.maximumForce = Mathf.Infinity;
            joint.xDrive = posDrive;
            joint.yDrive = posDrive;
            joint.zDrive = posDrive;
        }

        public override void Update() {
            base.Update();
            if (IsTriggerPressed()) {
                rotation += Time.deltaTime * 200f;
            } else {
                rotation += Time.deltaTime * 40f;
            }
        }
    }
}
