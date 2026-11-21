using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Vistara
{
    /// <summary>
    /// Keeps the UI attached to your sight and lets you get it out of the way:
    ///  - the menu panel follows you as you walk and turn (the results panel has its own follow-me; this module tightens it),
    ///  - a small dock sits in the lower-left of your view with "Hide UI / Show UI" and "Follow: on / off" (touch or poke them),
    ///  - shortcuts: left controller Y button = hide / show, right controller B button = follow on / off, H key in the editor.
    /// Hiding turns off every world-space panel (the dock stays), so you can look at Vee and the scene without anything in the way.
    /// </summary>
    public class PanelDock : MonoBehaviour
    {
        public static bool Hidden { get; private set; }
        public static bool Follow { get; private set; } = true;

        JourneyController journey; Canvas dock; TextMeshProUGUI hideLbl, followLbl, talkLbl; Transform camT;
        readonly List<Canvas> canvases = new List<Canvas>(); float nextScan; bool prevY, prevB;
        Vector3 offsetLocal, lastSet, vel; bool haveOffset, moving; float pauseUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name == "Journey" || s.name == "Workspace" || s.name == "MainMenu") Spawn(); };
            var n = SceneManager.GetActiveScene().name; if (n == "Journey" || n == "Workspace" || n == "MainMenu") Spawn();
        }
        static void Spawn() { if (FindFirstObjectByType<PanelDock>() == null) new GameObject("PanelDock").AddComponent<PanelDock>(); }

        void Start()
        {
            journey = FindFirstObjectByType<JourneyController>();
            dock = UIKit.WorldCanvas("PanelDock", 570, 96, null, 0.00048f); var rt = (RectTransform)dock.transform;
            var back = UIKit.Box(rt, "Back", new Color(0.03f, 0.05f, 0.12f, 0.82f), 22, false); UIKit.Fill(back.rectTransform); UIKit.Outline(back.transform, Theme.Cyan * new Color(1, 1, 1, 0.5f), 22, 2f);
            var b1 = UIKit.Button(rt, "", 12, 12, 172, 72, ToggleHidden, Theme.Card, Theme.Text, null, 24, 16); hideLbl = Label(b1.transform, "Hide UI");
            var b2 = UIKit.Button(rt, "", 196, 12, 172, 72, ToggleFollow, Theme.Card, Theme.Text, null, 24, 16); followLbl = Label(b2.transform, "Follow: on");
            var b3 = UIKit.Button(rt, "", 380, 12, 178, 72, PovView.Toggle, Theme.Card, Theme.Text, null, 24, 16); talkLbl = Label(b3.transform, "POV (A)");          // see through Vee's eyes          // voice chat with Vee (also: hold a controller grip)
        }

        static TextMeshProUGUI Label(Transform parent, string text)
        {
            var t = parent.GetComponentInChildren<TextMeshProUGUI>(); if (t == null) t = UIKit.TextAt(parent, "", 26, 0, 0, 172, 72, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            t.text = text; t.alignment = TextAlignmentOptions.Center; t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 26; return t;
        }

        public void ToggleHidden() { Hidden = !Hidden; Apply(); if (hideLbl != null) hideLbl.text = Hidden ? "Show UI" : "Hide UI"; }
        public void ToggleFollow() { Follow = !Follow; haveOffset = false; if (followLbl != null) followLbl.text = Follow ? "Follow: on" : "Follow: off"; }

        void Apply()
        {
            canvases.RemoveAll(c => c == null);
            foreach (var c in canvases)
            {
                if (c == dock) continue; c.enabled = !Hidden; var gr = c.GetComponent<GraphicRaycaster>(); if (gr != null) gr.enabled = !Hidden;
                var tr = c.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(); if (tr != null) tr.enabled = !Hidden;
            }
        }

        void Scan()
        {
            canvases.Clear(); foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.renderMode == RenderMode.WorldSpace && c != dock && !c.isRootCanvas == false) canvases.Add(c);
            if (Hidden) Apply();
        }

        static bool Button(XRNode node, UnityEngine.XR.InputFeatureUsage<bool> usage) { var d = InputDevices.GetDeviceAtXRNode(node); return d.isValid && d.TryGetFeatureValue(usage, out bool v) && v; }

        void Update()
        {
            if (journey == null) journey = FindFirstObjectByType<JourneyController>();
            var cam = Camera.main; if (cam == null) return; camT = cam.transform;
            // dock: locked to your view, lower-left, always readable
            if (dock != null) { var dt = dock.transform; dt.SetPositionAndRotation(camT.TransformPoint(new Vector3(-0.27f, -0.11f, 0.6f)), camT.rotation * Quaternion.Euler(8f, 0f, 0f)); dt.localScale = Vector3.one * 0.00048f; }
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.7f; Scan(); }
            bool y = Button(XRNode.LeftHand, UnityEngine.XR.CommonUsages.secondaryButton), b = Button(XRNode.RightHand, UnityEngine.XR.CommonUsages.secondaryButton);
            if (y && !prevY) ToggleHidden(); if (b && !prevB) ToggleFollow(); prevY = y; prevB = b;
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) ToggleHidden();
            if (talkLbl != null) talkLbl.text = PovView.Label;
            FollowStage(cam);
        }

        /// <summary>The menu panel (everything but the results viewer, which follows itself): keep it in front of you as you walk and turn.</summary>
        void FollowStage(Camera cam)
        {
            Transform root = journey != null ? journey.transform : (MainMenuController.Instance != null ? MainMenuController.Instance.transform : null);          // the journey panel, or the start menu
            if (!Follow || root == null) return; bool hud = journey != null && journey.Hud != null && journey.Hud.Visible; if (hud) { haveOffset = false; return; } Vector3 f = cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) return; f.Normalize(); Quaternion yaw = Quaternion.LookRotation(f, Vector3.up);
            if (!haveOffset) { offsetLocal = Quaternion.Inverse(yaw) * (root.position - cam.transform.position); haveOffset = true; lastSet = root.position; return; }
            if ((root.position - lastSet).sqrMagnitude > 0.0001f) { offsetLocal = Quaternion.Inverse(yaw) * (root.position - cam.transform.position); lastSet = root.position; pauseUntil = Time.unscaledTime + 1.2f; return; }     // you moved it by hand: keep it there
            if (Time.unscaledTime < pauseUntil) return;
            Vector3 desired = cam.transform.position + yaw * offsetLocal; float ang = Vector3.Angle(Vector3.ProjectOnPlane(root.forward, Vector3.up), f);
            if (ang > 22f || Vector3.Distance(root.position, desired) > 0.22f) moving = true; if (!moving) return;
            root.position = Vector3.SmoothDamp(root.position, desired, ref vel, 0.25f);
            Quaternion want = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(25f, 0f, 0f); root.rotation = Quaternion.Slerp(root.rotation, want, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            lastSet = root.position; if (Vector3.Distance(root.position, desired) < 0.03f) moving = false;
        }
    }
}
