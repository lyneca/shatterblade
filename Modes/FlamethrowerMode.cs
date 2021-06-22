using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;
using Newtonsoft.Json;
using Technie.PhysicsCreator;
using UnityEngine.AddressableAssets;
using Random = UnityEngine.Random;

namespace Shatterblade.Modes {
    class FlamethrowerMode : SpellMode<SpellCastProjectile> {
        private float rotation;
        private EffectInstance flameEffect;
        private EffectData revealFire;
        private List<EffectInstance> fireballEffects;
        private List<GameObject> fireballTargets;
        public override void OnItemLoaded(Item item) { base.OnItemLoaded(item); }
        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            fireballEffects = new List<EffectInstance>();
            fireballTargets = new List<GameObject>();
            revealFire = Catalog.GetData<EffectData>("ShatterbladeRevealFire");
        }

        Vector3 NormalPos(int i) {
            if (i == 1) {
                return Center();
            } else if (i == 2) {
                return Center() + UpDir() * 0.5f;
            } else if (i < 10) {
                return Center() + Quaternion.AngleAxis((i - 2) * (1f / 7f) * 360f + rotation, ForwardDir()) * UpDir() * 0.3f;
            } else {
                return Center() + Quaternion.AngleAxis((i - 9) * (1f / 5f) * 360f + rotation * -1, ForwardDir()) * UpDir() * 0.2f;
            }
        }

        Vector3 GetBasePos(int i) => Center()
                       + Quaternion.AngleAxis(i / 3f * 360f + rotation, ForwardDir())
                       * UpDir()
                       * 0.3f;

        Vector3 PressedPos(int i) {
            if (i == 1) {
                return Center() + ForwardDir() * 0.2f;
            } else if (i == 2) {
                return Center();
            } else {
                return GetBasePos((i - 2) / 4)
                       + ForwardDir() * 0.15f
                       + Quaternion.AngleAxis((i - 2) % 4 / 4f * 360f + rotation * -1.5f, ForwardDir())
                       * UpDir()
                       * 0.15f;
            }
        }

        public override Vector3 GetPos(int i, Rigidbody rb, BladePart part)
            => IsButtonPressed() ? PressedPos(i) : NormalPos(i);
        public override Quaternion GetRot(int i, Rigidbody rb, BladePart part) {
            if (i <= 2) {
                return Quaternion.LookRotation(ForwardDir(), UpDir());
            }
            if (IsButtonPressed()) {
                return Quaternion.LookRotation(
                    rb.transform.position
                    - GetBasePos((i - 2) / 4)
                    + ForwardDir() * (IsTriggerPressed() ? 0.25f : 015f),
                    ForwardDir());
            }

            return Quaternion.LookRotation(rb.transform.position - Center(), ForwardDir());
        }

        public override void OnTriggerPressed() {
            base.OnTriggerPressed();
            if (IsButtonPressed()) {
                fireballEffects.ForEach(effect => effect?.End());
                fireballEffects.Clear();
                fireballTargets.Clear();
                for (int subPos = 0; subPos < 3; subPos++) {
                    var target = new GameObject();
                    target.transform.position = GetBasePos(subPos) + ForwardDir() * 0.25f;
                    var effect = Catalog.GetData<EffectData>("SpellFireCharge")
                        .Spawn(target.transform, false);
                    effect.Play();
                    effect.SetIntensity(0);
                    fireballEffects.Add(effect);
                    fireballTargets.Add(target);
                }
            } else {
                flameEffect = Catalog.GetData<EffectData>("ShatterbladeFlamethrowerFlames").Spawn(sword.GetRB(1).transform.position, Quaternion.LookRotation(ForwardDir(), UpDir()), null, null, false);
                flameEffect.Play();
            }
        }

        public override void OnTriggerHeld() {
            base.OnTriggerHeld();
            rotation += Time.deltaTime * Mathf.Lerp(80, 300, Mathf.Clamp01((Time.time - lastTriggerPress) * (1f / 1f)));
            if (IsButtonPressed()) {
                int i = 0;
                fireballTargets.ForEach(target => target.transform.position = GetBasePos(i++) + ForwardDir() * 0.25f);
                fireballEffects.ForEach(effect => effect.SetIntensity(Mathf.Clamp01(Time.time - lastTriggerPress)));
            } else {
                if (flameEffect != null) {
                    flameEffect.SetPosition(sword.GetRB(1).transform.position);
                    flameEffect.SetRotation(Quaternion.LookRotation(ForwardDir(), UpDir()));
                    var creaturesHit = new List<Creature>();
                    foreach (var hit in Utils.ConeCastAll(Center(), 0.01f, ForwardDir() + Utils.RandomVector(-0.1f, 0.1f), 3, 10)) {
                        if (hit.rigidbody?.gameObject.GetComponent<RagdollPart>() is RagdollPart part) {
                            if (part.ragdoll.creature is Creature creature && !creature.isPlayer) {
                                if (!creaturesHit.Contains(part.ragdoll.creature)) {
                                    sword.RunAfter(() => {
                                        creaturesHit.Add(creature);
                                        var collisionInstance
                                            = new CollisionInstance(new DamageStruct(DamageType.Energy, 0f)
                                                { hitRagdollPart = part });
                                        collisionInstance.targetColliderGroup = part.colliderGroup;
                                        collisionInstance.contactPoint = hit.point;
                                        collisionInstance.contactNormal = hit.normal;
                                        collisionInstance.casterHand = Hand().caster;
                                        collisionInstance.hasEffect = true;
                                        collisionInstance.active = true;
                                        var effect = revealFire.Spawn(hit.point,
                                            Quaternion.AngleAxis(Random.Range(0, 360), hit.normal), part.transform,
                                            collisionInstance);
                                        effect.SetIntensity(Random.Range(0.5f, 1f));
                                        effect.Play();
                                        creature.Damage(collisionInstance);
                                    }, 0.5f);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void FireSpheres() {
            Catalog.GetData<ItemData>("FireSphere").SpawnAsync(sphere => {
                sphere.transform.position = Center() + ForwardDir() * 0.3f;
                sphere.rb.useGravity = false;
                sphere.rb.AddForce(ForwardDir() * 10f, ForceMode.Impulse);
                sphere.Despawn(1.5f);
            });
        }

        public void EndEffects() {
            fireballEffects.ForEach(effect => effect.End());
            fireballEffects.Clear();
            fireballTargets.Clear();
        }

        public void SpawnFireball(Vector3 position) {
            Catalog.GetData<ItemData>("DynamicProjectile").SpawnAsync(projectile => {
                projectile.transform.position = position;
                foreach (CollisionHandler collisionHandler in projectile.collisionHandlers) {
                    foreach (Damager damager in collisionHandler.damagers)
                        damager.Load(Catalog.GetData<DamagerData>("Fireball"), collisionHandler);
                }
                ItemMagicProjectile component = projectile.GetComponent<ItemMagicProjectile>();
                if (component) {
                    component.guided = false;
                    component.speed = 30;
                    component.item.lastHandler = Hand();
                    component.allowDeflect = true;
                    component.deflectEffectData = Catalog.GetData<EffectData>("HitFireBallDeflect");
                    component.imbueBladeEnergyTransfered = 0;
                    component.imbueSpellCastCharge = null;
                    component.Fire(Utils.HomingThrow(projectile, ForwardDir() * 30, 10f),
                        Catalog.GetData<EffectData>("SpellFireball"));
                } else {
                    projectile.rb.AddForce(Utils.HomingThrow(projectile, ForwardDir() * 30, 10f), ForceMode.Impulse);
                    projectile.Throw(flyDetection: Item.FlyDetection.Forced);
                }
            });
        }

        public void ThrowFireballs() {
            EndEffects();
            for (int i = 0; i < 3; i++) {
                SpawnFireball(GetBasePos(i) + ForwardDir() * 0.25f);
            }
        }

        public void CancelFireballs() {
            EndEffects();
        }

        public override void OnButtonReleased() {
            base.OnButtonReleased();
            if (IsTriggerPressed()) {
                if (Time.time - lastTriggerPress > 1f) {
                    ThrowFireballs();
                } else {
                    CancelFireballs();
                }
            }
        }

        public override void OnTriggerNotHeld() {
            base.OnTriggerNotHeld();
            rotation += Time.deltaTime * 80;
        }

        public override void OnTriggerReleased() {
            base.OnTriggerReleased();
            flameEffect?.End();
            if (IsButtonPressed()) {
                if (Time.time - lastTriggerPress > 1f) {
                    ThrowFireballs();
                } else {
                    CancelFireballs();
                }
            }
        }

        public override string GetUseAnnotation() => IsButtonPressed()
            ? (IsTriggerPressed() && Time.time - lastTriggerPress > 1f ? "Release to fire!" : "Pull trigger to charge fireballs!")
            : "Pull trigger to burn your foes";
        public override bool GetUseAnnotationShown() => true;

        public override string GetAltUseAnnotation() => "Hold button to switch modes";
        public override bool GetAltUseAnnotationShown() => !IsButtonPressed();

        public override void Update() {
            base.Update();
        }

        public override void Exit() {
            base.Exit();
            flameEffect?.End();
            CancelFireballs();
        }

        public override bool ShouldReform(BladePart part) => part != sword.GetPart(11);
    }
}
