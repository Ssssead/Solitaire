using System.Collections;
using UnityEngine;

public class MontanaAnimationService : MonoBehaviour
{
    private MontanaModeManager _mode;

    public void Initialize(MontanaModeManager mode)
    {
        _mode = mode;
    }

    public void ReorderAllContainers(System.Collections.Generic.IEnumerable<Transform> containers)
    {
        // В Ковре нет наложения карт Z-sorting не требуется.
    }
}