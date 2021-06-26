using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Random = UnityEngine.Random;

namespace Shatterblade {
    public class Annotation : MonoBehaviour {
        private TextMesh text;
        private Shatterblade sword;
        private LineRenderer lines;
        private Transform target;
        public Vector3 offset;
        private Transform rotationTarget;
        public bool shouldShow;

        public static string GetButtonString() => "A/X/Touchpad";
        public static string GetButtonStringHand(Side side) => (side == Side.Left ? "X" : "A") + "Touchpad";

        public static Annotation CreateAnnotation(Shatterblade sword, Transform target, Transform rotationTarget, Vector3 offset) {
            var annotation = new GameObject().AddComponent<Annotation>();
            annotation.Init(sword, target, rotationTarget, offset);
            return annotation;
        }

        public IEnumerator LoadData() {
            var textHandle = Addressables.LoadAssetAsync<Material>("Lyneca.Shatterblade.TextMat");
            yield return textHandle;
            text.GetComponent<MeshRenderer>().material = textHandle.Result;
            var fontHandle = Addressables.LoadAssetAsync<Font>("Lyneca.Shatterblade.Font");
            yield return fontHandle;
            text.font = fontHandle.Result;
            var lineHandle = Addressables.LoadAssetAsync<Material>("Lyneca.Shatterblade.LineMat");
            yield return lineHandle;
            lines.material = lineHandle.Result;
        }

        public void Init(Shatterblade sword, Transform target, Transform rotationTarget, Vector3 offset) {
            shouldShow = sword.isTutorialBlade;
            this.sword = sword;
            this.target = target;
            this.rotationTarget = target;
            this.offset = offset;
            text = gameObject.AddComponent<TextMesh>();
            text.alignment = TextAlignment.Center;
            text.text = "";
            transform.localScale = Vector3.one * 0.003f;
            text.anchor = TextAnchor.MiddleCenter;
            transform.position = TargetPosition();
            lines = gameObject.AddComponent<LineRenderer>();
            lines.startWidth = 0.001f;
            lines.endWidth = 0.001f;
            lines.startColor = Color.black;
            lines.endColor = Color.black;
            lines.colorGradient = new Gradient();
            lines.colorGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.grey, 0), new GradientColorKey(Color.black, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 1) });
            StartCoroutine(LoadData());
            if (!shouldShow) Hide();
        }

        public void SetTarget(Transform target) {
            this.target = target;
            this.rotationTarget = target;
        }

        public void SetText(string newText, RagdollHand hand = null) {
            if (!shouldShow) return;
            Show();
            text.text = newText.Replace("[[BUTTON]]", hand == null ? GetButtonString() : GetButtonStringHand(hand.side));
        }

        Camera PlayerCamera() => Player.local.head.cam;

        Vector3 TargetPosition() => target.position
                                    + rotationTarget.rotation * offset * 0.1f
                                    + (PlayerCamera().transform.position - target.position).normalized * 0.1f;

        public void Show() {
            if (!shouldShow) return;
            text.GetComponent<MeshRenderer>().enabled = true;
            lines.enabled = true;
        }

        public void Hide() {
            text.GetComponent<MeshRenderer>().enabled = false;
            lines.enabled = false;
        }

        public void Destroy() {
            Destroy(lines);
            text.text = "";
            Destroy(text);
            Destroy(this);
        }

        public void Update() {
            if (!shouldShow) return;
            if (sword.isDespawned) Destroy(this);
            transform.position = Vector3.Lerp(transform.position, TargetPosition(), Time.deltaTime * 10);
            transform.rotation = Quaternion.LookRotation(transform.position - PlayerCamera().transform.position);
            lines.SetPositions(new Vector3[2] { target.position, transform.position + transform.forward * 0.01f - transform.up * 0.01f });
        }
    }
}
