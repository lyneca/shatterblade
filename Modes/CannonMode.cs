using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;

namespace Shatterblade.Modes {
    class CannonMode : BladeMode {
        public List<Rigidbody> magazine;
        Rigidbody chamberedRound;
        float lastShot;
        float lastReload;
        bool buttonWasPressed = false;
        EffectInstance spinEffect;
        float rotation = 0;
        float lastButtonPressed;
        RagdollHand lastHand;
        private Annotation fireAnnotation;
        private Annotation burstAnnotation;
        private bool reloading;

        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            spinEffect = Catalog.GetData<EffectData>("ShatterbladeSpin").Spawn(GetHand().transform.position + GetHand().ThumbDir() * 0.1f, Quaternion.identity, null, null, false);
            sword.shouldLock = false;
            sword.animator.enabled = false;
            sword.jointRBs.ForEach(rb => rb.transform.parent = null);
            magazine = sword.jointRBs
                .Where(rb => rb.name != "Blade_1")
                .Where(rb => rb.name != "Blade_10")
                .ToList();
            sword.ReformParts();
            sword.GetPart(10).Detach();
            sword.IgnoreRagdoll(Player.currentCreature.ragdoll, true);
            lastHand = GetHand();
            ChamberRound();
            fireAnnotation = Annotation.CreateAnnotation(sword, sword.GetPart(10).transform,
                sword.GetPart(10).transform, GetFireAnnotationPosition());
            burstAnnotation = Annotation.CreateAnnotation(sword, sword.GetPart(10).transform,
                sword.GetPart(10).transform, GetBurstAnnotationPosition());
            sword.HideAllAnnotations();
        }

        public Vector3 GetFireAnnotationPosition()
            => GetHand().side == Side.Left ? new Vector3(1, 0.7f, 1) : new Vector3(1, 0.7f, -1);
        public Vector3 GetBurstAnnotationPosition()
            => GetHand().side == Side.Left ? new Vector3(-1, 0.7f, 1) : new Vector3(-1, 0.7f, -1);
        public RagdollHand GetHand() => sword.GetPart(10).item.handlers.FirstOrDefault();
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
            magazine.Remove(chamberedRound);
        }
        public void ChamberRound() {
            if (!magazine.Any())
                return;
            ChamberRound(magazine[Random.Range(0, magazine.Count())]);
        }
        public void Fire(Rigidbody round) {
            sword.rbMap[round].Detach();
            sword.rbMap[round].item.rb.AddForce(Utils.HomingThrow(sword.rbMap[round].item, AimDir() * 60f, 30), ForceMode.Impulse);
            sword.rbMap[round].item.Throw(1, Item.FlyDetection.Forced);
        }
        public void Fire() {
            Catalog.GetData<EffectData>("ShatterbladeShoot").Spawn(chamberedRound.position, chamberedRound.rotation, null, null, false).Play();
            Fire(chamberedRound);
            chamberedRound = null;
            ChamberRound();
        }
        Vector3 AimDir() => GetHand().PointDir();
        Vector3 UpDir() => GetHand().ThumbDir();
        Vector3 GetPosition(float index) {
            return GetHand().transform.position + GetHand().ThumbDir() * 0.1f
                 + Quaternion.AngleAxis(
                     index / magazine.Count() * 360
                     + rotation, AimDir())
                 * UpDir() * (GetHand().playerHand.controlHand.alternateUsePressed ? 0.2f : 0.3f);
        }
        public override void Update() {
            base.Update();
            if (GetHand() != lastHand) {
                sword.IgnoreCollider(lastHand, false);
                lastHand = GetHand();
                sword.IgnoreCollider(lastHand, true);
            }

            fireAnnotation.offset = GetFireAnnotationPosition();
            burstAnnotation.offset = GetBurstAnnotationPosition();
            if (GetHand().playerHand.controlHand.alternateUsePressed) {
                rotation += Time.deltaTime
                    * Mathf.Lerp(20, 400, Mathf.Clamp01(Time.time - lastButtonPressed) * 2);
                fireAnnotation.SetText("Pull trigger to burst fire");
                burstAnnotation.Hide();
            } else {
                fireAnnotation.SetText("Pull trigger to fire");
                burstAnnotation.SetText("Hold Oculus A/X to charge up a burst shot");
                rotation += Time.deltaTime
                    * Mathf.Lerp(400, 80, Mathf.Clamp01((Time.time - lastShot) * 0.5f));
            }
            // 'magazine'
            foreach (var bullet in magazine) {
                bullet.transform.position = GetPosition(magazine.IndexOf(bullet));
                bullet.transform.rotation = Quaternion.LookRotation(GetHand().PointDir(), bullet.transform.position - GetHand().transform.position)
                      * Quaternion.Inverse(sword.rbMap[bullet].item.GetFlyDirRefLocalRotation());
            }
            // gun 'barrel'
            if (sword.GetPart(1) is BladePart partOne && sword.partMap[partOne]) {
                sword.partMap[partOne].transform.position = GetHand().transform.position + GetHand().ThumbDir() * 0.07f + GetHand().PointDir() * 0.2f;
                sword.partMap[partOne].transform.rotation
                    = Quaternion.LookRotation(AimDir(), GetHand().PalmDir())
                    * Quaternion.Inverse(partOne.item.GetFlyDirRefLocalRotation());
;
            }
            // bullet
            if (chamberedRound && sword.rbMap[chamberedRound]?.transform?.rotation != null) {
                chamberedRound.transform.position = GetHand().transform.position + GetHand().ThumbDir() * 0.15f;
                chamberedRound.transform.rotation = Quaternion.LookRotation(AimDir(), UpDir())
                    * Quaternion.Inverse(sword.rbMap[chamberedRound].item.GetFlyDirRefLocalRotation());
            }
            spinEffect.SetPosition(GetHand().transform.position + GetHand().ThumbDir() * 0.1f);
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
            if (GetHand().playerHand.controlHand.alternateUsePressed) {
                if (!buttonWasPressed) {
                    buttonWasPressed = true;
                    lastButtonPressed = Time.time;
                    spinEffect = Catalog.GetData<EffectData>("ShatterbladeSpin").Spawn(GetHand().transform.position + GetHand().ThumbDir() * 0.1f, Quaternion.identity, null, null, false);
                    spinEffect.Play();
                }
            } else {
                buttonWasPressed = false;
                spinEffect.End();
            }
            if (GetHand().playerHand.controlHand.usePressed) {
                if (Time.time - lastShot > 0.4f) {
                    Fire();
                    if (GetHand().playerHand.controlHand.alternateUsePressed) {
                        lastShot = Time.time - 0.35f;
                    } else {
                        lastShot = Time.time;
                    }
                }
            } else {
                lastShot = 0;
            }
        }
        public override void Exit() {
            base.Exit();
            sword.jointRBs.ForEach(rb => rb.transform.parent = sword.animator.transform);
            sword.animator.enabled = true;
            fireAnnotation.Destroy();
            burstAnnotation.Destroy();
            spinEffect?.Despawn();
            sword.IgnoreRagdoll(Player.currentCreature.ragdoll, false);
            sword.shouldLock = true;
        }
        public override bool ShouldReform(BladePart part) => (magazine.Contains(sword.partMap[part]) && part != sword.GetPart(10))
                                                          || part == sword.GetPart(1);
    }
}
