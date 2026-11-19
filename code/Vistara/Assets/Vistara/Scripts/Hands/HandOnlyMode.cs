using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Vistara
{
    /// <summary>
    /// Hands-only interaction for the Journey scene: the template XRInputModalityManager normally switches between hands and controllers;
    /// here the controller objects are removed from it and deactivated on device builds, so selection comes from the hand pinch (XR Hands +
    /// OpenXR Meta hand aim) only. In the editor the XR Interaction Simulator keeps working: it starts in controller mode, so switch it to
    /// Hand mode (see README) or use the mouse on the world UI.
    /// </summary>
    public class HandOnlyMode : MonoBehaviour
    {
        public static string Report = "";

        void Start()
        {
            var mm = FindFirstObjectByType<XRInputModalityManager>();
            if (mm == null) { Report = "no XRInputModalityManager in scene"; Debug.LogWarning("[Vistara] " + Report); return; }
#if !UNITY_EDITOR
            if (mm.leftController != null) mm.leftController.SetActive(false);
            if (mm.rightController != null) mm.rightController.SetActive(false);
            mm.leftController = null; mm.rightController = null;
            Report = "hands only (controllers removed from the modality manager)";
#else
            Report = "editor: simulator keeps controller mode available; hands are enabled";
#endif
            Debug.Log("[Vistara] HandOnlyMode: " + Report + "; hand objects present: " + (mm.leftHand != null && mm.rightHand != null));
            // direct finger interaction: the hand poke interactors and the near-far (pinch) interactors must be allowed to drive world-space UI
            int poke = 0, nearFar = 0;
            foreach (var p in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { p.enableUIInteraction = true; poke++; }
            foreach (var n in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { n.enableUIInteraction = true; nearFar++; }
            Debug.Log("[Vistara] UI interaction enabled on " + poke + " poke interactor(s) and " + nearFar + " near-far interactor(s)");
            HandSwipeDetector.Ensure();
            HandTouchInput.Ensure();                       // direct fingertip touch / pinch on the panels
        }
    }
}
