using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade.Modes {
    class CannonMode : GrabbedShardMode {
        public float fireRate = 0.4f;
        public float fireVelocity = 60;

        List<Rigidbody> magazine;
        Rigidbody chamberedRound;
        float lastShot;
        float lastReload;
        EffectInstance spinEffect;
        float rotation = 0;
        RagdollHand lastHand;
        bool reloading;

        public override int TargetPartNum() => 10;

        public override Vector3 Center() {
            return Hand().transform.position + UpDir() * 0.1f;
        }

        public override void Enter(Shatterblade sword) {
            magazine = sword.jointRBs
                .Where(rb => rb.name != "Blade_1")
                .Where(rb => rb.name != "Blade_10")
                .ToList();
            base.Enter(sword);
            spinEffect = Catalog.GetData<EffectData>("ShatterbladeSpin").Spawn(Center(), Quaternion.identity, null, null, false);
            sword.IgnoreRagdoll(Player.currentCreature.ragdoll, true);
            lastHand = Hand();
            ChamberRound();
        }

        public override bool GetUseAnnotationShown() => true;
        public override string GetUseAnnotation() => IsButtonPressed() ? "Pull trigger to burst fire" : "Pull trigger to fire";
        public override bool GetAltUseAnnotationShown() => !IsButtonPressed();

        public override string GetAltUseAnnotation() => "Hold [[BUTTON]] to charge up a burst shot";

        public void Reload() {
            if (Time.time - lastReload <= 1)
                return;
            lastReload = Time.time;
            reloading = true;
            magazine = sword.jointRBs
                .Where(rb => rb.name != "Blade_1")
                .Where(rb => rb.name != "Blade_10")
                .ToList();
        }
        public void ChamberRound(Rigidbody round) {
            chamberedRound = null;
            if (!magazine.Any())
                return;
            chamberedRound = round;
            Hand().HapticTick(0.2f, 100f);
            magazine.Remove(chamberedRound);
        }
        public void ChamberRound() {
            if (!magazine.Any())
                return;
            ChamberRound(magazine[Random.Range(0, magazine.Count())]);
        }
        public void Fire(Rigidbody round) {
            Hand().PlayHapticClipOver(AnimationCurve.EaseInOut(0, 1, 1, 0), 0.2f);
            sword.rbMap[round].Detach();
            sword.rbMap[round].item.rb.AddForce(Utils.HomingThrow(sword.rbMap[round].item, ForwardDir() * fireVelocity, 30), ForceMode.Impulse);
            sword.rbMap[round].item.Throw(1, Item.FlyDetection.Forced);
        }
        public void Fire() {
            if (!chamberedRound) return;
            Catalog.GetData<EffectData>("ShatterbladeShoot").Spawn(chamberedRound.position, chamberedRound.rotation, null, null, false).Play();
            Fire(chamberedRound);
            chamberedRound = null;
            ChamberRound();
        }

        public override Vector3 GetPos(int index, Rigidbody rb, BladePart part) {
            if (rb == chamberedRound) {
                return Center() + UpDir() * 0.05f;
            } else if (index == 1) {
                return Hand().transform.position + Hand().ThumbDir() * 0.07f + Center() + ForwardDir() * 0.2f;
            } else if (magazine.Any()) {
                return Center()
                       + Quaternion.AngleAxis(
                           magazine.IndexOf(rb) * 360f / magazine.Count()
                           + rotation, ForwardDir())
                       * UpDir()
                       * (IsButtonPressed() ? 0.2f : 0.3f);
            } else {
                return Center()
                       + UpDir()
                       * (IsButtonPressed() ? 0.2f : 0.3f);
            }
        }

        public override Quaternion GetRot(int index, Rigidbody rb, BladePart part) {
            if (rb == chamberedRound) {
                return Quaternion.LookRotation(ForwardDir(), UpDir());
            } else if (index == 1) {
                return Quaternion.LookRotation(ForwardDir(), SideDir());
            } else {
                return Quaternion.LookRotation(ForwardDir(), rb.transform.position - Center());
            }
        }

        public override void OnButtonHeld() {
            base.OnButtonHeld();
            rotation += Time.deltaTime
                * Mathf.Lerp(20, 400, Mathf.Clamp01(Time.time - lastButtonPress) * 2);
        }

        public override void OnButtonNotHeld() {
            base.OnButtonNotHeld();
            rotation += Time.deltaTime
                * Mathf.Lerp(400, 80, Mathf.Clamp01((Time.time - lastShot) * 0.5f));
        }

        public override void OnButtonPressed() {
            base.OnButtonPressed();
            spinEffect = Catalog.GetData<EffectData>("ShatterbladeSpin").Spawn(Center(), Quaternion.identity, null, null, false);
            spinEffect.Play();
        }

        public override void OnButtonReleased() {
            base.OnButtonReleased();
            spinEffect.End();
        }

        public override void OnTriggerHeld() {
            base.OnTriggerHeld();
            if (Time.time - lastShot > fireRate) {
                Fire();
                if (IsButtonPressed()) {
                    lastShot = Time.time - (fireRate - 0.05f);
                } else {
                    lastShot = Time.time;
                }
            }
        }

        public override void OnTriggerReleased() {
            base.OnTriggerReleased();
            lastShot = 0;
        }

        public override void Update() {
            base.Update();
            if (Hand() != lastHand) {
                sword.IgnoreCollider(lastHand, false);
                lastHand = Hand();
                sword.IgnoreCollider(lastHand, true);
            }

            spinEffect.SetPosition(Center());
            if (!magazine.Any() && !chamberedRound) {
                Reload();
            }

            if (reloading) {
                if (magazine.All(part => Vector3.Distance(sword.rbMap[part].transform.position, sword.GetPart(10).transform.position) < 1f)) {
                    reloading = false;
                } else {
                    return;
                }
            }
            if (chamberedRound == null)
                ChamberRound();
        }
        public override void Exit() {
            base.Exit();
            sword.jointRBs.ForEach(rb => rb.transform.parent = sword.animator.transform);
            sword.animator.enabled = true;
            spinEffect?.Despawn();
            sword.IgnoreRagdoll(Player.currentCreature.ragdoll, false);
            sword.shouldLock = true;
        }
        public override bool ShouldReform(BladePart part) => (magazine.Contains(sword.partMap[part]) && part != sword.GetPart(10))
                                                          || part == sword.GetPart(1);
    }
}
