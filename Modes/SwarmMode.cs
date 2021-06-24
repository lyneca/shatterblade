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

        public override Vector3 Center() => base.Center() + ForwardDir() * 1.5f * (1 + Hand().playerHand.controlHand.useAxis);

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

        public override Vector3 GetPos(int index, Rigidbody rb, BladePart part) {
            float size = IsButtonPressed() ? 0.5f : 1f;

            Vector3 pos = Utils.UniqueVector(rb.gameObject, -size, size);
            Vector3 normal = Utils.UniqueVector(part.item.gameObject, -size, size, 1);
            Quaternion handAngle = Quaternion.LookRotation(SideDir());
            return Center() + handAngle * pos.Rotated(Quaternion.AngleAxis(Time.time * 120, normal));
        }

        public override Quaternion GetRot(int index, Rigidbody rb, BladePart part) {
            float size = IsButtonPressed() ? 0.5f : 1f;
            Vector3 pos = Utils.UniqueVector(rb.gameObject, -size, size);
            pos = pos.normalized * (pos.magnitude + 0.2f);
            Vector3 normal = Utils.UniqueVector(part.item.gameObject, -size, size, 1);
            Quaternion handAngle = Quaternion.LookRotation(SideDir());
            Vector3 facingDir = Center() + handAngle * pos.Rotated(Quaternion.AngleAxis(rotation, normal)) - rb.transform.position;
            return Quaternion.LookRotation((Hand().Velocity() + facingDir).normalized);
        }
        public override bool ShouldLock(BladePart part) => false;

        public override void JointModifier(ConfigurableJoint joint, BladePart part) {
            var posDrive = joint.xDrive;
            posDrive.positionSpring = 100;
            posDrive.positionDamper = 10;
            posDrive.maximumForce = 1000;
            joint.xDrive = posDrive;
            joint.yDrive = posDrive;
            joint.zDrive = posDrive;
        }

        public override void Update() {
            base.Update();
            rotation += Time.deltaTime * 200f * (1 + Hand().Velocity().magnitude / 3f);
        }
    }
}
