using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

using ExtensionMethods;
using System.Collections;

namespace ExtensionMethods {
    public enum FingerPart {
        Proximal,
        Intermediate,
        Distal
    }
    static class ExtensionMethods {
        /// <summary>Get raw angular velocity of the player hand</summaryt
        public static bool IsEmpty(this RagdollHand hand) {
            return !hand.caster.isFiring
                && !hand.caster.isMerging
                && !Player.currentCreature.mana.mergeActive
                && hand.grabbedHandle == null
                && hand.caster.telekinesis.catchedHandle == null;
        }

        public static int Capacity(this Holder holder) => holder.data.maxQuantity;

        /// <summary>
        ///  Get hand local angular velocity
        /// </summary>
        public static Vector3 LocalAngularVelocity(this RagdollHand hand) => hand.transform.InverseTransformDirection(hand.rb.angularVelocity);

        public static Task<TOutput> Then<TInput, TOutput>(this Task<TInput> task, Func<TInput, TOutput> func) {
            return task.ContinueWith((input) => func(input.Result));
        }
        public static Task Then(this Task task, Action<Task> func) {
            return task.ContinueWith(func);
        }
        public static Task Then<TInput>(this Task<TInput> task, Action<TInput> func) {
            return task.ContinueWith((input) => func(input.Result));
        }

        /// <summary>
        /// Get a component from the gameobject, or create it if it doesn't exist
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
            return obj.GetComponent<T>() ?? obj.AddComponent<T>();
        }

        /// <summary>
        /// Force this WhooshPoint to play its effect
        /// </summary>
        public static void Play(this WhooshPoint point) {
            if ((point.GetField("trigger") is WhooshPoint.Trigger trigger) && trigger != WhooshPoint.Trigger.OnGrab && point.GetField("effectInstance") != null)
                (point.GetField("effectInstance") as EffectInstance)?.Play();
            Utils.SetInstanceField(point, "effectActive", true);
            Utils.SetInstanceField(point, "dampenedIntensity", 0);
        }

        /// <summary>
        /// Attempt to point an item's FlyDirRef at a target vector
        /// </summary>
        /// <param name="target">Target vector</param>
        /// <param name="lerpFactor">Lerp factor (if you're calling over multiple frames)</param>
        /// <param name="upDir">Up direction</param>
        public static void PointItemFlyRefAtTarget(this Item item, Vector3 target, float lerpFactor, Vector3? upDir = null) {
            Vector3 up = upDir ?? Vector3.up;
            if (item.flyDirRef) {
                item.transform.rotation = Quaternion.Slerp(
                    item.transform.rotation * item.flyDirRef.localRotation,
                    Quaternion.LookRotation(target, up),
                    lerpFactor) * Quaternion.Inverse(item.flyDirRef.localRotation);
            } else if (item.holderPoint) {
                item.transform.rotation = Quaternion.Slerp(
                    item.transform.rotation * item.holderPoint.localRotation,
                    Quaternion.LookRotation(target, up),
                    lerpFactor) * Quaternion.Inverse(item.holderPoint.localRotation);
            } else {
                Quaternion pointDir = Quaternion.LookRotation(item.transform.up, up);
                item.transform.rotation = Quaternion.Slerp(item.transform.rotation * pointDir, Quaternion.LookRotation(target, up), lerpFactor) * Quaternion.Inverse(pointDir);
            }
        }

        /// <summary>
        /// Is is this hand gripping?
        /// </summary>
        public static bool IsGripping(this RagdollHand hand) => hand?.playerHand?.controlHand?.gripPressed ?? false;
        public static void HapticTick(this RagdollHand hand, float intensity = 1, float frequency = 10) => PlayerControl.input.Haptic(hand.side, intensity, frequency);
        public static void PlayHapticClipOver(this RagdollHand hand, AnimationCurve curve, float duration) {
            hand.StartCoroutine(HapticPlayer(hand, curve, duration));
        }
        public static IEnumerator HapticPlayer(RagdollHand hand, AnimationCurve curve, float duration) {
            var time = Time.time;
            while (Time.time - time < duration) {
                hand.HapticTick(curve.Evaluate((Time.time - time) / duration));
                yield return 0;
            }
        }

        /// <summary>
        /// Return the minimum entry in an interator using a custom comparable function
        /// </summary>
        public static T MinBy<T>(this IEnumerable<T> enumerable, Func<T, IComparable> comparator) {
            if (!enumerable.Any())
                return default;
            return enumerable.Aggregate((curMin, x) => (curMin == null || (comparator(x).CompareTo(comparator(curMin)) < 0)) ? x : curMin);
        }

        /// <summary>
        /// .Select(), but only when the output of the selection function is non-null
        /// </summary>
        public static IEnumerable<TOut> SelectNotNull<TIn, TOut>(this IEnumerable<TIn> enumerable, Func<TIn, TOut> func)
            => enumerable.Where(item => func(item) != null).Select(func);
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> enumerable)
            => enumerable.Where(item => item != null);

        /// <summary>
        /// Get a point above the player's hand
        /// </summary>
        public static Vector3 PosAboveBackOfHand(this RagdollHand hand) => hand.transform.position
            - hand.transform.right * 0.1f
            + hand.transform.forward * 0.2f;
        public static Vector3 PalmDir(this RagdollHand hand) => hand.transform.forward * -1;
        public static Vector3 PointDir(this RagdollHand hand) => -hand.transform.right;
        public static Transform IndexTip(this RagdollHand hand) => hand.fingerIndex.distal.collider.transform;
        public static Vector3 Palm(this RagdollHand hand) => hand.transform.position + hand.PointDir() * 0.1f;
        public static Vector3 Velocity(this RagdollHand hand) => Player.local.transform.rotation * hand.playerHand.controlHand.GetHandVelocity();
        public static float MapOverCurve(this float time, params Tuple<float, float>[] points) {
            var curve = new AnimationCurve();
            foreach (var point in points) {
                curve.AddKey(new Keyframe(point.Item1, point.Item2));
            }
            return curve.Evaluate(time);
        }
        public static float MapOverCurve(this float time, params Tuple<float, float, float, float>[] points) {
            var curve = new AnimationCurve();
            foreach (var point in points) {
                curve.AddKey(new Keyframe(point.Item1, point.Item2, point.Item3, point.Item4));
            }
            return curve.Evaluate(time);
        }

        /// <summary>
        /// Vector pointing in the direction of the thumb
        /// </summary>
        public static Vector3 ThumbDir(this RagdollHand hand) => (hand.side == Side.Right) ? hand.transform.up : -hand.transform.up;

        /// <summary>
        /// Clamp a number between -1000 and 1000, just in case
        /// </summary>
        public static float SafetyClamp(this float num) => Mathf.Clamp(num, -1000, 1000);

        /// <summary>
        /// I miss Rust's .abs()
        /// </summary>
        public static float Abs(this float num) => Mathf.Abs(num);

        /// <summary>
        /// float.SafetyClamp() but for vectors
        /// </summary>
        public static Vector3 SafetyClamp(this Vector3 vec) => vec.normalized * vec.magnitude.SafetyClamp();

        /// <summary>
        /// Returns true if the vector's X component is its largest component
        /// </summary>
        public static bool MostlyX(this Vector3 vec) => vec.x.Abs() > vec.y.Abs() && vec.x.Abs() > vec.z.Abs();

        /// <summary>
        /// Returns true if the vector's Y component is its largest component
        /// </summary>
        public static bool MostlyY(this Vector3 vec) => vec.y.Abs() > vec.x.Abs() && vec.y.Abs() > vec.z.Abs();

        /// <summary>
        /// Returns true if the vector's Z component is its largest component
        /// </summary>
        public static bool MostlyZ(this Vector3 vec) => vec.z.Abs() > vec.x.Abs() && vec.z.Abs() > vec.y.Abs();

        /// <summary>
        /// Get a creature's part from a PartType
        /// </summary>
        public static RagdollPart GetPart(this Creature creature, RagdollPart.Type partType) => creature.ragdoll.GetPart(partType);

        /// <summary>
        /// Get a creature's head
        /// </summary>
        public static RagdollPart GetHead(this Creature creature) => creature.ragdoll.headPart;

        /// <summary>
        /// Get a creature's torso
        /// </summary>
        public static RagdollPart GetTorso(this Creature creature) => creature.GetPart(RagdollPart.Type.Torso);
        public static Vector3 GetChest(this Creature creature) => Vector3.Lerp(creature.GetTorso().transform.position, creature.GetHead().transform.position, 0.5f);

        public static float HandVelocityInLocalDirection(this RagdollHand hand, Vector3 direction) {
            return Vector3.Dot(hand.Velocity(), hand.transform.TransformDirection(direction));
        }
        public static float HandVelocityInDirection(this RagdollHand hand, Vector3 direction) {
            return Vector3.Dot(hand.Velocity(), direction);
        }
        public static Vector3 Rotated(this Vector3 vector, Quaternion rotation, Vector3 pivot = default) {
            return rotation * (vector - pivot) + pivot;
        }
        public static Side Other(this Side side) {
            return side == Side.Left ? Side.Right : Side.Left;
        }
        public static Vector3 Rotated(this Vector3 vector, Vector3 rotation, Vector3 pivot = default) {
            return Rotated(vector, Quaternion.Euler(rotation), pivot);
        }

        public static Vector3 Rotated(this Vector3 vector, float x, float y, float z, Vector3 pivot = default) {
            return Rotated(vector, Quaternion.Euler(x, y, z), pivot);
        }
        public static bool IsFacing(this Vector3 source, Vector3 other) => Vector3.Angle(source, other) < 50;
        public static void SetPosition(this EffectInstance instance, Vector3 position) {
            instance.effects.ForEach(effect => effect.transform.position = position);
        }
        public static void SetRotation(this EffectInstance instance, Quaternion rotation) {
            instance.effects.ForEach(effect => effect.transform.rotation = rotation);
        }
        public static void SetScale(this EffectInstance instance, Vector3 scale) {
            foreach (var effect in instance.effects) {
                if (effect is EffectMesh mesh) {
                    mesh.transform.localScale = scale;
                    mesh.meshSize = scale;
                }
            }
        }
        public static void RunCoroutine(this MonoBehaviour mono, Func<IEnumerator> function, float delay = 0) {
            if (mono.isActiveAndEnabled) {
                mono.StartCoroutine(RunAfterCoroutine(function, delay));
            }
        }
        public static void RunAfter(this MonoBehaviour mono, System.Action action, float delay = 0) {
            if (mono.isActiveAndEnabled) {
                mono.StartCoroutine(RunAfterCoroutine(action, delay));
            }
        }
        public static void RunNextFrame(this MonoBehaviour mono, System.Action action) {
            if (mono.isActiveAndEnabled) {
                mono.StartCoroutine(RunNextFrameCoroutine(action));
            }
        }
        public static IEnumerator RunAfterCoroutine(Func<IEnumerator> function, float delay) {
            yield return new WaitForSeconds(delay);
            yield return function();
        }
        public static IEnumerator RunAfterCoroutine(System.Action action, float delay) {
            yield return new WaitForSeconds(delay);
            action();
        }
        public static IEnumerator RunNextFrameCoroutine(System.Action action) {
            yield return 0;
            action();
        }
        public static GameObject AddComponents<T>(this GameObject obj, Action<T> callback) where T : Component {
            callback(obj.AddComponent<T>());
            return obj;
        }
        public static RagdollHand.Finger GetFinger(this RagdollHand hand, Finger finger) {
            switch (finger) {
                case Finger.Thumb:
                    return hand.fingerThumb;
                case Finger.Index:
                    return hand.fingerIndex;
                case Finger.Middle:
                    return hand.fingerMiddle;
                case Finger.Ring:
                    return hand.fingerRing;
                case Finger.Little:
                    return hand.fingerLittle;
            }
            return null;
        }
        public static Transform GetFingerPart(this RagdollHand.Finger finger, FingerPart part) {
            switch (part) {
                case FingerPart.Proximal:
                    return finger.proximal.collider.transform;
                case FingerPart.Intermediate:
                    return finger.intermediate.collider.transform;
                case FingerPart.Distal:
                    return finger.distal.collider.transform;
            }
            return null;
        }

        public static object Call(this object o, string methodName, params object[] args) {
            var mi = o.GetType().GetMethod(methodName, BindingFlags.Instance);
            if (mi != null) {
                return mi.Invoke(o, args);
            }
            return null;
        }
        // This method is ILLEGAL
        public static object CallPrivate(this object o, string methodName, params object[] args) {
            var mi = o.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null) {
                return mi.Invoke(o, args);
            }
            return null;
        }
        public static object GetField(this object instance, string fieldName) {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        public static void SetSpring(this ConfigurableJoint joint, float spring) {
            var drive = joint.xDrive;
            drive.positionSpring = spring;
        }
        public static void SetDamping(this ConfigurableJoint joint, float damper) {
            var drive = joint.xDrive;
            drive.positionDamper = damper;
        }
        public static float GetMassModifier(this Item item) {
            if (item.rb.mass < 1) {
                return item.rb.mass * 3;
            } else {
                return item.rb.mass;
            }
        }
        public static Item UnSnapOne(this Holder holder, bool silent) {
            Item obj = holder.items.LastOrDefault();
            if (obj)
                holder.UnSnap(obj, silent);
            return obj;
        }

        //public static Vector3 GetBounds(this Item item) {
        //    var filter = item.renderers
        //        .Select(renderer => renderer.gameObject.GetComponent<MeshFilter>()).OrderBy(meshFilter
        //            => (meshFilter.transform.position - item.transform.position + meshFilter.mesh.bounds.extents).magnitude)
        //        .Last();
        //    var localRotation = Quaternion.Inverse(item.transform.rotation) * filter.transform.rotation;
        //    return Quaternion.Inverse(localRotation) * filter.mesh.bounds.extents * filter.transform.localScale;
        //}

        public static Vector3 GetScaleRelativeTo(this Transform transform, Transform target) {
            Vector3 output = Vector3.one;
            var parent = transform;
            while (parent.parent != target && parent.parent != null) {
                output = output.MultiplyComponents(parent.localScale);
                parent = parent.parent;
            }

            return output;
        }


        public static Vector3 MultiplyComponents(this Vector3 a, Vector3 b)
            => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        public static float GetRadius(this Item item) => item.renderers
            .Select(renderer => renderer.gameObject.GetComponent<MeshFilter>()).Max(meshFilter
                => meshFilter.transform.GetScaleRelativeTo(item.transform).MultiplyComponents(
                        meshFilter.transform.position - item.transform.position + meshFilter.mesh.bounds.extents)
                    .magnitude);
        public static void Depenetrate(this Item item) {
            item.collisionHandlers.ForEach(ch => ch.damagers.ForEach(damager => damager.UnPenetrateAll()));
        }
        public static void SetVFXProperty<T>(this EffectInstance effect, string name, T data) {
            if (data is Vector3 vec3) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx)) {
                    fx.vfx.SetVector3(name, vec3);
                }
            } else if (data is float flt) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx)) {
                    fx.vfx.SetFloat(name, flt);
                }
            } else if (data is int integer) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx)) {
                    fx.vfx.SetInt(name, integer);
                }
            } else if (data is bool boolean) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx)) {
                    fx.vfx.SetBool(name, boolean);
                }
            } else if (data is Texture texture) {
                foreach (EffectVfx fx in effect.effects.Where(fx => fx is EffectVfx)) {
                    fx.vfx.SetTexture(name, texture);
                }
            }
        }
        public static Quaternion GetFlyDirRefLocalRotation(this Item item) => Quaternion.Inverse(item.transform.rotation) * item.flyDirRef.rotation;
    }
}
static class Utils {

    public static ConfigurableJoint CreateSimpleJoint(Rigidbody source, Rigidbody target, float spring, float damper) {
        Quaternion orgRotation = source.transform.rotation;
        source.transform.rotation = target.transform.rotation;
        var joint = source.gameObject.AddComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.targetRotation = Quaternion.identity;
        joint.anchor = source.centerOfMass;
        joint.connectedAnchor = target.centerOfMass;
        joint.connectedBody = target;
        JointDrive posDrive = new JointDrive {
            positionSpring = spring,
            positionDamper = damper,
            maximumForce = Mathf.Infinity
        };
        JointDrive rotDrive = new JointDrive {
            positionSpring = 1000,
            positionDamper = 10,
            maximumForce = Mathf.Infinity
        };
        joint.rotationDriveMode = RotationDriveMode.XYAndZ;
        joint.xDrive = posDrive;
        joint.yDrive = posDrive;
        joint.zDrive = posDrive;
        joint.angularXDrive = rotDrive;
        joint.angularYZDrive = rotDrive;
        source.transform.rotation = orgRotation;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;
        joint.xMotion = ConfigurableJointMotion.Free;
        joint.yMotion = ConfigurableJointMotion.Free;
        joint.zMotion = ConfigurableJointMotion.Free;
        return joint;
    }

    // WARNING: If you can find a way to not use the following two methods, please do - they are INCREDIBLY bad practice
    /// <summary>
    /// Get a private field from an object
    /// </summary>
    public static void Explosion(Vector3 origin, float force, float radius, bool massCompensation = false, bool disarm = false) {
        var seenRigidbodies = new List<Rigidbody>();
        var seenCreatures = new List<Creature> { Player.currentCreature };
        foreach (var collider in Physics.OverlapSphere(origin, radius)) {
            if (collider.attachedRigidbody == null)
                continue;
            if (collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerHandAndFoot) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotion) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotionObject))
                continue;
            if (!seenRigidbodies.Contains(collider.attachedRigidbody)) {
                seenRigidbodies.Add(collider.attachedRigidbody);
                float modifier = 1;
                if (collider.attachedRigidbody.mass < 1) {
                    modifier *= collider.attachedRigidbody.mass * 2;
                } else {
                    modifier *= collider.attachedRigidbody.mass;
                }
                if (!massCompensation)
                    modifier = 1;
                collider.attachedRigidbody.AddExplosionForce(force * modifier, origin, radius, 1, ForceMode.Impulse);
            } else if (collider.GetComponentInParent<Creature>() is Creature npc && npc != null && !seenCreatures.Contains(npc)) {
                seenCreatures.Add(npc);
                npc.brain.instance.TryPush((npc.ragdoll.rootPart.transform.position - origin).normalized, npc.ragdoll.creature.brain.instance.gravityPushBehaviorPerLevel[2]);
                if (disarm) {
                    npc.handLeft.TryRelease();
                    npc.handRight.TryRelease();
                }
            }
        }
    }
    public static void PushForce(Vector3 origin, Vector3 direction, float radius, float distance, Vector3 force, bool massCompensation = false, bool disarm = false) {
        var seenRigidbodies = new List<Rigidbody>();
        var seenCreatures = new List<Creature> { Player.currentCreature };
        foreach (var hit in Physics.SphereCastAll(origin, radius, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
            var collider = hit.collider;
            if (collider.attachedRigidbody == null)
                continue;
            if (collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerHandAndFoot) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotion) ||
               collider.attachedRigidbody.gameObject.layer == GameManager.GetLayer(LayerName.PlayerLocomotionObject))
                continue;
            if (!seenRigidbodies.Contains(collider.attachedRigidbody)) {
                seenRigidbodies.Add(collider.attachedRigidbody);
                float modifier = 1;
                if (collider.attachedRigidbody.mass < 1) {
                    modifier *= collider.attachedRigidbody.mass * 2;
                } else {
                    modifier *= collider.attachedRigidbody.mass;
                }
                if (!massCompensation)
                    modifier = 1;
                collider.attachedRigidbody.AddForce(force * modifier, ForceMode.Impulse);
            } else if (collider.GetComponentInParent<Creature>() is Creature npc && npc != null && !seenCreatures.Contains(npc)) {
                seenCreatures.Add(npc);
                npc.brain.instance.TryPush((npc.ragdoll.rootPart.transform.position - origin).normalized, npc.ragdoll.creature.brain.instance.gravityPushBehaviorPerLevel[2]);
                if (disarm) {
                    npc.handLeft.TryRelease();
                    npc.handRight.TryRelease();
                }
            }
        }
    }

    public static Vector3 HomingThrow(Item item, Vector3 velocity, float homingAngle) {
        var hits = Physics.SphereCastAll(item.transform.position, 10, velocity, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        var targets = hits.SelectNotNull(hit => hit.collider?.attachedRigidbody?.GetComponentInParent<Creature>())
            .Where(creature => creature != Player.currentCreature && creature.state != Creature.State.Dead)
            .Where(creature => Vector3.Angle(velocity, creature.ragdoll.headPart.transform.position - item.transform.position)
                 < homingAngle + 3 * Vector3.Distance(item.transform.position, Player.currentCreature.transform.position))
            .OrderBy(creature => Vector3.Angle(velocity, creature.ragdoll.headPart.transform.position - item.transform.position));
        var closeToAngle = targets.Where(creature => Vector3.Angle(velocity, creature.ragdoll.headPart.transform.position - item.transform.position) < 5);
        if (closeToAngle.Any()) {
            targets = closeToAngle.OrderBy(creature => Vector3.Distance(item.transform.position, creature.ragdoll.headPart.transform.position));
        }
        var target = targets.FirstOrDefault();
        if (!target)
            return velocity;
        var extendedPoint = item.transform.position + velocity.normalized * Vector3.Distance(item.transform.position, target.ragdoll.GetPart(RagdollPart.Type.Torso).transform.position);
        var targetPart = target.ragdoll.parts.MinBy(part => Vector3.Distance(part.transform.position, extendedPoint));
        var vectorToTarget = targetPart.transform.position - item.transform.position;
        item.rb.velocity = Vector3.zero;
        velocity = vectorToTarget.normalized * velocity.magnitude;
        return velocity;
    }

    public static Transform GetPlayerChest() {
        return Player.currentCreature.ragdoll.GetPart(RagdollPart.Type.Torso).transform;
    }

    public static Vector3 UniqueVector(GameObject obj, float min = -1, float max = 1, int salt = 0) {
        var rand = new System.Random(obj.GetInstanceID() + salt);
        return new Vector3(
            (float)rand.NextDouble() * (max - min) + min,
            (float)rand.NextDouble() * (max - min) + min,
            (float)rand.NextDouble() * (max - min) + min);
    }

    public static Vector3 RandomVector(float min = -1, float max = 1, int salt = 0) {
        return new Vector3(
            UnityEngine.Random.Range(0f, 1f) * (max - min) + min,
            UnityEngine.Random.Range(0f, 1f) * (max - min) + min,
            UnityEngine.Random.Range(0f, 1f) * (max - min) + min);
    }

    /// <summary>
    /// Set a private field from an object
    /// </summary>
    internal static void SetInstanceField<T, U>(T instance, string fieldName, U value) {
        BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static;
        FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
        field.SetValue(instance, value);
    }

    /// <summary>
    /// Get a list of live NPCs
    /// </summary>
    public static IEnumerable<Creature> GetAliveNPCs() => Creature.list
        .Where(creature => creature != Player.currentCreature
                        && creature.state != Creature.State.Dead);

    public static IEnumerable<Creature> ConeCastCreature(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, float coneAngle, bool npc = true, bool live = true) {
        return ConeCastAll(origin, maxRadius, direction, maxDistance, coneAngle)
            .SelectNotNull(hit => hit.rigidbody?.gameObject.GetComponent<Creature>())
            .Where(creature => (!npc || creature != Player.currentCreature) && (!live || creature.state != Creature.State.Dead));
    }
    public static IEnumerable<RagdollPart> ConeCastRagdollPart(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, float coneAngle, bool npc = true, bool live = true) {
        return ConeCastAll(origin, maxRadius, direction, maxDistance, coneAngle)
            .SelectNotNull(hit => hit.rigidbody?.gameObject.GetComponent<RagdollPart>())
            .Where(part => (!npc || part.ragdoll.creature != Player.currentCreature) && (!live || part.ragdoll.creature.state != Creature.State.Dead));
    }
    public static IEnumerable<Item> ConeCastItem(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, float coneAngle) {
        return ConeCastAll(origin, maxRadius, direction, maxDistance, coneAngle)
            .SelectNotNull(hit => hit.rigidbody?.gameObject.GetComponent<Item>())
            .Where(item => !item.isTelekinesisGrabbed && item.holder == null && item.mainHandler == null);
    }
    public static IEnumerable<Creature> SphereCastCreature(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, bool npc = true, bool live = true) {
        return Physics.SphereCastAll(origin, maxRadius, direction, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .SelectNotNull(hit => hit.rigidbody?.gameObject.GetComponent<Creature>())
            .Where(creature => (!npc || creature != Player.currentCreature) && (!live || creature.state != Creature.State.Dead));
    }
    public static IEnumerable<Item> OverlapSphereItem(Vector3 origin, float radius) {
        return Physics.OverlapSphere(origin, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .SelectNotNull(collider => collider.GetComponentInParent<Item>());
    }

    public static void UpdateDriveStrengths(this ConfigurableJoint joint, float strength) {
        if (joint == null)
            return;
        JointDrive posDrive = new JointDrive();
        posDrive.positionSpring = 100 * strength;
        posDrive.positionDamper = 10 * strength;
        posDrive.maximumForce = 1000;
        JointDrive rotDrive = new JointDrive();
        rotDrive.positionSpring = 10 * strength;
        rotDrive.positionDamper = 1 * strength;
        rotDrive.maximumForce = 100;
        joint.xDrive = posDrive;
        joint.yDrive = posDrive;
        joint.zDrive = posDrive;
        joint.angularXDrive = rotDrive;
        joint.angularYZDrive = rotDrive;
    }

    // Original idea from walterellisfun on github: https://github.com/walterellisfun/ConeCast/blob/master/ConeCastExtension.cs
    /// <summary>
    /// Like SphereCastAll but in a cone
    /// </summary>
    /// <param name="origin">Origin position</param>
    /// <param name="maxRadius">Maximum cone radius</param>
    /// <param name="direction">Cone direction</param>
    /// <param name="maxDistance">Maximum cone distance</param>
    /// <param name="coneAngle">Cone angle</param>
    public static RaycastHit[] ConeCastAll(Vector3 origin, float maxRadius, Vector3 direction, float maxDistance, float coneAngle) {
        RaycastHit[] sphereCastHits = Physics.SphereCastAll(origin, maxRadius, direction, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        List<RaycastHit> coneCastHitList = new List<RaycastHit>();

        if (sphereCastHits.Length > 0) {
            for (int i = 0; i < sphereCastHits.Length; i++) {
                Vector3 hitPoint = sphereCastHits[i].point;
                Vector3 directionToHit = hitPoint - origin;
                float angleToHit = Vector3.Angle(direction, directionToHit);
                bool hitRigidbody = sphereCastHits[i].rigidbody is Rigidbody rb
                                    && Vector3.Angle(direction, rb.transform.position - origin) < coneAngle;

                if (angleToHit < coneAngle || hitRigidbody) {
                    coneCastHitList.Add(sphereCastHits[i]);
                }
            }
        }
        return coneCastHitList.ToArray();
    }
}
