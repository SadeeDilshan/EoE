using UnityEngine;

public class SoilPatchSlot : MonoBehaviour
{
    [Header("Soil Settings")]
    public bool isFertile = false; // Check TRUE on only ONE soil patch!
    public bool isScanned = false;

    [Header("Visual Feedback Materials (Optional)")]
    public Material scannedGoodMat; // Green glow
    public Material scannedBadMat;  // Red/Brown toxic look

    public string ScanSoil()
    {
        isScanned = true;
        MeshRenderer rend = GetComponent<MeshRenderer>();

        if (isFertile)
        {
            if (rend != null && scannedGoodMat != null) rend.material = scannedGoodMat;
            return "SCAN COMPLETE: 98% Rich Nutrients! Press [E] again to Plant Sapling (Place [F] Barriers first!)";
        }
        else
        {
            if (rend != null && scannedBadMat != null) rend.material = scannedBadMat;
            return "SCAN COMPLETE: Toxic Heavy Metals Detected! Cannot plant here—find another soil patch.";
        }
    }
}