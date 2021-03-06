using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade.Modes {
    class ExpandedMode : BladeMode {
        public override bool Test(Shatterblade sword) => false;

        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            sword.animator.SetBool("IsExpanded", true);
            sword.ReformParts();
        }

        public override void Update() {
            base.Update();
            sword.handleAnnotationA.SetText("Release [[BUTTON]] to retract the blade");
            sword.handleAnnotationB.SetText("Hold Trigger to form a shield");
            sword.imbueHandleAnnotation.Hide();
            sword.otherHandAnnotation.Hide();
            sword.gunShardAnnotation.Hide();
        }

        public override void Exit() {
            base.Exit();
            sword.animator.SetBool("IsExpanded", false);
        }
    }
}
