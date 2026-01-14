using UnityEngine;

public class TutorialHighlighter : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Color originalEmission;
    private Material targetMaterial;

    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private float intensity = 2.0f;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        targetMaterial = meshRenderer.material;
        
        originalEmission = targetMaterial.GetColor("_EmissionColor");
    }

    public void SetHighlight(bool active)
    {
        if (active)
        {
            targetMaterial.SetColor("_EmissionColor", highlightColor * intensity);
            targetMaterial.EnableKeyword("_EMISSION");
        }
        else
        {
            targetMaterial.SetColor("_EmissionColor", originalEmission);
            if (originalEmission == Color.clear || originalEmission == Color.black)
                targetMaterial.DisableKeyword("_EMISSION");
        }
    }
}