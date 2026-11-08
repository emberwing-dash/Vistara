using UnityEngine;

namespace Vistara
{
    /// <summary>Places the UI rig (and aligns the neon environment) in front of the XR camera once its tracked pose has settled.</summary>
    public class PlaceInFront : MonoBehaviour
    {
        public float distance = 2.0f; public float heightOffset = -0.1f;
        public bool Placed { get; private set; }
        float t, stable; Vector3 last; bool trackedPlaced;

        static Camera FindCam()
        {
            var c = Camera.main;
            if (c != null) return c;
            foreach (var cam in Camera.allCameras) if (cam.enabled && cam.stereoTargetEye != StereoTargetEyeMask.None) return cam;
            return Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
        }

        void Update()
        {
            if (trackedPlaced) return;
            t += Time.unscaledDeltaTime;
            var cam = FindCam();
            if (cam == null) return;
            stable = (cam.transform.position - last).sqrMagnitude < 0.0004f ? stable + Time.unscaledDeltaTime : 0f;
            last = cam.transform.position;
            bool tracked = cam.transform.position.y > 0.6f;       // HMD pose is valid once it reports a head height
            if (tracked && stable > 0.3f && t > 0.3f) { Recenter(); trackedPlaced = true; }   // ideal: valid head pose
            else if (!Placed && t > 12f) Recenter();                                              // fallback; re-placed once tracking becomes valid
        }

        public void Recenter()
        {
            var cam = FindCam(); if (!cam) return;
            Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            transform.position = cam.transform.position + f * distance + Vector3.up * heightOffset;
            transform.rotation = Quaternion.LookRotation(f, Vector3.up);
            var env = GameObject.Find("Environment");      // keep the neon corridor aligned with where the user looks
            if (env) { env.transform.position = new Vector3(cam.transform.position.x, 0f, cam.transform.position.z); env.transform.rotation = Quaternion.LookRotation(f, Vector3.up); }
            Placed = true;
            Debug.Log("[Vistara] UI placed in front of " + cam.name + " at " + transform.position);
        }
    }
}
