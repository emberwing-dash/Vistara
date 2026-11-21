using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Vistara
{
    /// <summary>
    /// Character point of view: press A (right controller) and you see through Vee's eyes - what she is doing, from where she does it - without walking after her.
    /// Press A again to come back to where you were. (P in the editor, or the POV button of the dock.)
    /// Your head still looks around freely; the view follows her eyes and turns when she turns.
    /// </summary>
    public class PovView : MonoBehaviour
    {
        public static bool On { get; private set; }
        static PovView instance;
        XROrigin origin; Vector3 savedPos; Quaternion savedRot; bool prevA; float lastYaw;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name == "Journey" || s.name == "Workspace") Spawn(); else On = false; };
            var n = SceneManager.GetActiveScene().name; if (n == "Journey" || n == "Workspace") Spawn();
        }
        static void Spawn() { On = false; if (instance == null) { instance = new GameObject("PovView").AddComponent<PovView>(); } }

        public static void Toggle() { if (instance != null) instance.Flip(); }

        void Flip()
        {
            if (origin == null) origin = FindFirstObjectByType<XROrigin>();
            var g = GuideGirl.Instance; if (origin == null || origin.Origin == null || g == null || g.HeadT == null) return;
            var ot = origin.Origin.transform;
            if (!On)
            {
                savedPos = ot.position; savedRot = ot.rotation; On = true;
                Vector3 f = g.transform.forward; f.y = 0; if (f.sqrMagnitude > 1e-4f) origin.MatchOriginUpCameraForward(Vector3.up, f.normalized);
                lastYaw = g.transform.eulerAngles.y;
            }
            else { On = false; ot.SetPositionAndRotation(savedPos, savedRot); }
        }

        void Update()
        {
            var d = InputDevices.GetDeviceAtXRNode(XRNode.RightHand); bool a = d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool v) && v;
            if (a && !prevA) Flip(); prevA = a;
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame) Flip();
        }

        void LateUpdate()
        {
            if (!On) return; var g = GuideGirl.Instance;
            if (origin == null || g == null || g.HeadT == null) { On = false; return; }
            FreeRoam.SuspendUntil = Time.unscaledTime + 0.2f;          // no walking while you are looking through her eyes
            float yaw = g.transform.eulerAngles.y; float dy = Mathf.DeltaAngle(lastYaw, yaw); lastYaw = yaw;
            if (Mathf.Abs(dy) > 0.01f) origin.RotateAroundCameraUsingOriginUp(dy);
            Vector3 f = g.transform.forward; f.y = 0; f.Normalize();
            origin.MoveCameraToWorldLocation(g.HeadT.position + Vector3.up * 0.06f + f * 0.11f);
        }

        public static string Label { get { return On ? "POV on (A)" : "POV (A)"; } }
    }
}
