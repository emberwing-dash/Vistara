using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Vistara
{
    /// <summary>
    /// Hands AND controllers: the template XRInputModalityManager switches between them. Hands pinch / touch the panels (HandTouchInput);
    /// controllers point and pull the trigger (ControllerPointer). The toolkit's own UI pointers are off on both so a press is never delivered twice.
    /// </summary>
    public class HandOnlyMode : MonoBehaviour
    {
        public static string Report = "";

        void Start()
        {
            ControllerPointer.Ensure();                    // controller ray + trigger on the panels
            var mm = FindFirstObjectByType<XRInputModalityManager>();
            if (mm == null) { Report = "no XRInputModalityManager in scene"; Debug.LogWarning("[Vistara] " + Report); return; }
            if (mm.leftHand != null) mm.leftHand.SetActive(false); if (mm.rightHand != null) mm.rightHand.SetActive(false);          // controllers only: no hand mode, no hand switching
            mm.leftHand = null; mm.rightHand = null;
            Report = "controllers: point at a button and pull the trigger";
            Debug.Log("[Vistara] HandOnlyMode: " + Report + "; hand objects present: " + (mm.leftHand != null && mm.rightHand != null));
            // direct finger interaction: the hand poke interactors and the near-far (pinch) interactors must be allowed to drive world-space UI
            int poke = 0, nearFar = 0;
            foreach (var p in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { if (ControllerPointer.IsController(p)) continue; p.enableUIInteraction = true; poke++; }
            foreach (var n in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { if (ControllerPointer.IsController(n)) continue; n.enableUIInteraction = true; nearFar++; }
            Debug.Log("[Vistara] UI interaction enabled on " + poke + " poke interactor(s) and " + nearFar + " near-far interactor(s)");
            HandSwipeDetector.Ensure();
            HandTouchInput.Ensure();                       // direct fingertip touch / pinch on the panels
        }
    }
}
