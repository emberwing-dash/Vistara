using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Vistara
{
    /// <summary>
    /// Walk around the space and look at Vee up close (the scenes ship without a locomotion provider):
    /// left thumbstick = move (relative to where you look), right thumbstick = turn (smooth), click the left stick to move faster.
    /// Without controllers (XR Device Simulator / hands only) nothing changes: the simulator already moves the headset, and room-scale walking works as usual.
    /// The rig is moved directly, so props never block you (the worlds have no colliders) and you cannot fall.
    /// </summary>
    public class FreeRoam : MonoBehaviour
    {
        public float walkSpeed = 1.5f, runSpeed = 3.2f, turnSpeed = 90f;
        public static float SuspendUntil;          // set by ControllerPointer while a ray points at a panel
        XROrigin origin; float nextFind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name == "Journey" || s.name == "Workspace") Spawn(); };
            var n = SceneManager.GetActiveScene().name; if (n == "Journey" || n == "Workspace") Spawn();
        }
        static void Spawn() { if (FindFirstObjectByType<FreeRoam>() == null) new GameObject("FreeRoam").AddComponent<FreeRoam>(); }

        static bool Stick(XRNode node, out Vector2 v, out bool click)
        {
            v = Vector2.zero; click = false; var d = InputDevices.GetDeviceAtXRNode(node); if (!d.isValid) return false;
            d.TryGetFeatureValue(CommonUsages.primary2DAxis, out v); d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out click); return true;
        }

        void Update()
        {
            if (origin == null) { if (Time.time < nextFind) return; nextFind = Time.time + 1f; origin = FindFirstObjectByType<XROrigin>(); if (origin == null) return; }
            var cam = origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null; if (cam == null) return;
            if (Time.unscaledTime < SuspendUntil) return;
            bool hasL = Stick(XRNode.LeftHand, out var mv, out var run); bool hasR = Stick(XRNode.RightHand, out var tn, out _);
            if (hasL && mv.sqrMagnitude > 0.04f)
            {
                Vector3 f = cam.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = cam.up; f.Normalize(); Vector3 r = cam.right; r.y = 0f; r.Normalize();
                Vector3 d = (f * mv.y + r * mv.x) * (run ? runSpeed : walkSpeed) * Time.deltaTime;
                var cc = origin.GetComponent<CharacterController>(); if (cc != null && cc.enabled) cc.Move(d); else origin.transform.position += d;
            }
            if (hasR && Mathf.Abs(tn.x) > 0.25f) origin.transform.RotateAround(cam.position, Vector3.up, tn.x * turnSpeed * Time.deltaTime);
        }
    }
}
