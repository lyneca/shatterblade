using System.Linq;
using UnityEngine;
using ThunderRoad;
using ExtensionMethods;
using Newtonsoft.Json;
using Random = UnityEngine.Random;

namespace Shatterblade.Modes {
    class LightningMode : SpellMode<SpellCastLightning> {
        public float cooldown = 1;
        public float damage = 50;
        public float explosionForce = 50;
        public float explosionRadius = 3;

        private float rotation;
        private GameObject target;
        private EffectInstance chargeEffect;
        private bool wasPressed;

        public override void OnItemLoaded(Item item) { base.OnItemLoaded(item); }
        public override void Enter(Shatterblade sword) {
            base.Enter(sword);
            Debug.Log(explosionForce);
            target = new GameObject();
        }
        float CooldownTime() => 1 - Mathf.Clamp01(Mathf.InverseLerp(0, cooldown, Time.time - lastTriggerReleased));

        public override Vector3 GetPos(int index, Rigidbody rb, BladePart part) {
            switch (index) {
                case 1:
                    return Center() + ForwardDir() * (0.1f + CooldownTime() * 0.1f);
                case 2:
                    return Center() + ForwardDir() * (0.3f + CooldownTime() * 0.2f);
                case int n when n < 10:
                    return Hand().Palm()
                           + Quaternion.AngleAxis((index - 2) * (1f / 7f) * 360f + rotation, ForwardDir())
                           * UpDir()
                           * (0.15f + CooldownTime() * 0.1f)
                           + ForwardDir() * -0.2f
                           + Random.Range(-1f, 1f) * ForwardDir() * (IsTriggerPressed() ? 0.1f : 0) * CooldownTime();
                default:
                    return Hand().Palm()
                           + Quaternion.AngleAxis((index - 10) * (1f / 5f) * 360f + rotation * -1, ForwardDir())
                           * UpDir()
                           * (0.1f + CooldownTime() * 0.1f)
                           + Random.Range(-1f, 1f) * ForwardDir() * (IsTriggerPressed() ? 0.1f : 0) * Mathf.Clamp01(Time.time - lastTriggerPress);
            }
        }

        public override string GetUseAnnotation() => IsTriggerPressed() ? "Release to fire" : "Hold Trigger to charge";
        public override bool GetUseAnnotationShown() => true;

        public override Quaternion GetRot(int index, Rigidbody rb, BladePart part) {
            switch (index) {
                case 1:
                    return Quaternion.LookRotation(ForwardDir(), SideDir());
                case 2:
                    return Quaternion.LookRotation(ForwardDir(), -SideDir());
                case int n when n < 10:
                    return Quaternion.LookRotation(rb.transform.position - (Hand().Palm() - ForwardDir() * -0.2f), ForwardDir());
                default:
                    return Quaternion.LookRotation(ForwardDir(), rb.transform.position - Hand().Palm());
            }
        }

        public override float Cooldown() => 1f;

        public override void OnTriggerPressed() {
            base.OnTriggerPressed();
            chargeEffect = Catalog.GetData<EffectData>("ShatterbladeLightningCharge").Spawn(Center(), Quaternion.identity);
            chargeEffect.Play();
        }

        public override void OnTriggerHeld() {
            base.OnTriggerHeld();
            wasPressed = true;
            rotation += Time.deltaTime * Mathf.Lerp(80, 300, Mathf.Clamp01(Time.time - lastTriggerPress));
            chargeEffect?.SetIntensity(Mathf.Clamp01(Time.time - lastTriggerPress));
            chargeEffect?.SetPosition(Center());
            Hand().HapticTick(Mathf.Clamp01(Time.time - lastTriggerPress) * 0.5f, 20);
        }

        public override void OnTriggerNotHeld() {
            base.OnTriggerNotHeld();
            rotation += Time.deltaTime * 80 * (1 + CooldownTime() * 4);
        }

        public override void OnTriggerReleased() {
            base.OnTriggerReleased();
            if (!wasPressed) return;
            wasPressed = false;
            var effect = Catalog.GetData<EffectData>("ShatterbladeLightning").Spawn(Center(), Quaternion.identity);
            chargeEffect.End();
            var part = GetRagdollPartHit();
            var point = GetTargetPoint();
            target.transform.position = part?.transform.position ?? point;
            effect.SetSource(GetPart().transform);
            effect.SetTarget(target.transform);
            effect.Play();
            if (part) {
                var collisionInstance
                    = new CollisionInstance(new DamageStruct(DamageType.Energy, damage) {
                        hitRagdollPart = part,
                        recoil = 10f
                    }) {
                        casterHand = Hand().caster,
                        impactVelocity = (target.transform.position - Center()).normalized * 30f,
                        contactPoint = target.transform.position,
                        contactNormal = (target.transform.position - Center()).normalized,
                        targetColliderGroup = part.colliderGroup
                    };
                part.ragdoll.creature.Damage(collisionInstance);
                ActionShock action = part.ragdoll.creature.brain.GetAction<ActionShock>();
                if (action != null) {
                    action.Refresh(1, 5);
                } else {
                    ActionShock actionShock = new ActionShock(1, 5,
                        Catalog.GetData<EffectData>(Catalog.GetData<SpellCastLightning>("Lightning")
                            .imbueHitRagdollEffectId));
                    part.ragdoll.creature.brain.TryAction(actionShock, true);
                }
            }

            Utils.Explosion(target.transform.position + ForwardDir() * -0.5f, explosionForce, explosionRadius, true, true, true);
        }

        Vector3 GetTargetPoint() {
            var hits = Physics
                .RaycastAll(Center(), ForwardDir(), 50, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(hit => hit.rigidbody?.GetComponentInParent<BladePart>() == null
                              && !(hit.rigidbody?.GetComponentInParent<RagdollPart>()?.ragdoll.creature.isPlayer
                                  ?? false))
                .OrderBy(hit => Vector3.Distance(hit.point, Center()));
            if (hits.Any()) return hits.First().point;
            return Center() + ForwardDir() * 50;
        }

        RagdollPart GetRagdollPartHit() => Utils.ConeCastRagdollPart(Center(), 5, ForwardDir(), 50, 30, true, true)
            .OrderBy(
                part => Vector3.Distance(part.transform.position, Center()) / 30 * 2
                        * Vector3.Angle(ForwardDir(), part.transform.position - Center()) / 50)
            .FirstOrDefault();

        public override void Update() {
            base.Update();
        }

        public override void Exit() {
            chargeEffect?.End();
            base.Exit();
        }
    }
}
