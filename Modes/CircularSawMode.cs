using ThunderRoad;
using UnityEngine;
using ExtensionMethods;


namespace Shatterblade.Modes {
    class CircularSawMode : GrabbedShardMode {
        public override int TargetPartNum() => 12;
        float rotation;
        public override Vector3 Center() => base.Center() + ForwardDir() * 0.4f;
        public override Vector3 GetPos(int index, Rigidbody rb, BladePart part) {
            if (index > 9)
                return Center()
                       + Quaternion.AngleAxis((index - 10) * 360f / 5f - rotation / 3, SideDir())
                       * UpDir()
                       * 0.1f;
            return Center()
                   + Quaternion.AngleAxis(index * 360f / 9f + rotation, SideDir())
                   * UpDir()
                   * (IsTriggerPressed() ? 0.2f : 0.25f);
        }
        public override Quaternion GetRot(int index, Rigidbody rb, BladePart part)
            => Quaternion.LookRotation(rb.transform.position - Center(), SideDir());
        public override string GetUseAnnotation() => "Press trigger to spin";
        public override bool GetUseAnnotationShown() => !IsTriggerPressed();
        public override void Update() {
            base.Update();
            if (IsTriggerPressed()) {
                rotation += Time.deltaTime * 700;
            } else {
                rotation += Time.deltaTime * 80;
            }
        }
    }
}
