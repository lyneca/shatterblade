using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade {
    class SwordMode : BladeMode {
        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            sword.ReformParts();
        }

        public override void Update() {
            base.Update();
            if (sword.locking) {
                sword.handleAnnotationA.SetText("Hold A/X to expand\nthe blade");
                sword.gunShardAnnotation.SetText("Grab this shard to\nmake a handgun!");
                if (sword.TopShardIsImbued()) {
                    sword.imbueShardAnnotation.Hide();
                    sword.imbueHandleAnnotation.SetText("Now that you've imbued the\ntop shard, grab this piece");
                } else {
                    sword.imbueHandleAnnotation.Hide();
                    sword.imbueShardAnnotation.SetText("Imbue this shard with\nFire, Gravity or Lightning\nfor different effects");
                }
            } else {
                sword.handleAnnotationA.Hide();
            }
            sword.handleAnnotationB.SetText($"Tap A/X to {(sword.locking ? "Shatter" : "Reform")}\n the blade");
        }
    }
}
