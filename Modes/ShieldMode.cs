using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade {
    class ShieldMode : BladeMode {
        public override bool Test(Shatterblade sword) => false;
        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            sword.animator.SetBool("IsExpanded", false);
            sword.animator.SetBool("IsLeft", sword.buttonHand.PalmDir().IsFacing(sword.item.transform.forward));
            sword.animator.SetBool("IsShield", true);
            sword.ReformParts();
            sword.handleAnnotationA.Hide();
            sword.handleAnnotationB.Hide();
            sword.imbueHandleAnnotation.Hide();
            sword.otherHandAnnotation.Hide();
            sword.gunShardAnnotation.Hide();
        }
        public override void Exit() {
            base.Exit();
            sword.animator.SetBool("IsShield", false);
        }
    }
}
