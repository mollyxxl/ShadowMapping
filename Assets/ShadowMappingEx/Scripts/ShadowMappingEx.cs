using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ShadowMappingEx : MonoBehaviour
{
    public enum CustomShadowResolution
    {
        Low = 256,
        Middle = 512,
        High = 1024,
        VeryHigh = 2048,
    }
    public enum ShadowsType
    { 
        NONE,
        HARD,
        PCF,
        VARIANCE,
        MOMENT
    }

    [Header("默认设置")]
    [SerializeField]
    Shader _depthShader;

    [SerializeField]
    ComputeShader _blur;

    public Light dirLight;

    [Header("阴影质量")]
    [SerializeField]
    CustomShadowResolution _resolution = CustomShadowResolution.High;
    public FilterMode _filterMode = FilterMode.Bilinear;
    public ShadowsType _shadowType = ShadowsType.HARD;
    [Range(0, 100)]
    public int blurIterations = 1;

    [Range(0, 1)]
    public float shadowStrength = 0.6f;

    public bool drawTransparent = true;

    [Range(0, 1)]
    public float varianceShadowExpansion = 0.3f;


    // Render Targets
    Camera _shadowCam;
    RenderTexture _backTarget;
    RenderTexture _target;



    void Update()
    {
        _depthShader = _depthShader ? _depthShader : Shader.Find("ShadowMap/Custom/Depth");
        SetUpShadowCam();
        UpdateRenderTexture();
        UpdateShadowCameraPosition();

        _shadowCam.targetTexture = _target;
        _shadowCam.RenderWithShader(_depthShader, "");

        if (_shadowType == ShadowsType.VARIANCE)
        {
            for (int i = 0; i < blurIterations; i++)
            {
                _blur.SetTexture(0, "Read", _target);
                _blur.SetTexture(0, "Result", _backTarget);
                _blur.Dispatch(0, _target.width / 8, _target.height / 8, 1);

                Swap(ref _backTarget, ref _target);
            }
        }

        UpdateShaderArgs();
    }
    void OnDisable()
    {
        if (_shadowCam)
        {
            DestroyImmediate(_shadowCam.gameObject);
            _shadowCam = null;
        }

        if (_target)
        {
            DestroyImmediate(_target);
            _target = null;
        }

        if (_backTarget)
        {
            DestroyImmediate(_backTarget);
            _backTarget = null;
        }

        ForAllKeywords(s => Shader.DisableKeyword(ToKeyword(s)));
    }
     void OnDestroy()
    {
        OnDisable();
    }
    void SetUpShadowCam()
    {
        if (_shadowCam)
            return;
        GameObject go = new GameObject("Shadow Camera");
        go.hideFlags = HideFlags.DontSave;

        _shadowCam = go.AddComponent<Camera>();
        _shadowCam.orthographic = true;
        _shadowCam.nearClipPlane = 0;
        _shadowCam.enabled = false;
        _shadowCam.backgroundColor = new Color(0, 0, 0, 0);
        _shadowCam.clearFlags = CameraClearFlags.SolidColor;
    }
    void UpdateRenderTexture()
    {
        if (_target != null && (_target.width != (int)_resolution || _target.filterMode != _filterMode))
        {
            DestroyImmediate(_target);
            _target = null;
        }

        if (_target == null)
        {
            _target = CreateTarget();
            _backTarget = CreateTarget();
        }

    }
    void UpdateShadowCameraPosition()
    {
        Camera cam = _shadowCam;
        if (dirLight == null)
        {
            dirLight = FindObjectOfType<Light>();
        }

        cam.transform.position = dirLight.transform.position;
        cam.transform.rotation = dirLight.transform.rotation;
        cam.transform.LookAt(cam.transform.position + cam.transform.forward, cam.transform.up);

        Vector3 center, extents;
        List<Renderer> renderers = new List<Renderer>();
        renderers.AddRange(FindObjectsOfType<Renderer>());

        GetRenderersExtents(renderers, cam.transform, out center, out extents);

        center.z -= extents.z / 2;
        cam.transform.position = cam.transform.TransformPoint(center);
        cam.nearClipPlane = 0;
        cam.farClipPlane = extents.z;

        cam.aspect = extents.x / extents.y;
        cam.orthographicSize = extents.y / 2;
    }
    void UpdateShaderArgs()
    {
        ForAllKeywords(s => Shader.DisableKeyword(ToKeyword(s)));
        Shader.EnableKeyword(ToKeyword(_shadowType));

        // Set the qualities of the textures
        Shader.SetGlobalTexture("_ShadowTex", _target);
        Shader.SetGlobalMatrix("_LightMatrix", _shadowCam.transform.worldToLocalMatrix);
        Shader.SetGlobalFloat("_MaxShadowIntensity", shadowStrength);
        Shader.SetGlobalFloat("_VarianceShadowExpansion", varianceShadowExpansion);

        if (drawTransparent) Shader.EnableKeyword("DRAW_TRANSPARENT_SHADOWS");
        else Shader.DisableKeyword("DRAW_TRANSPARENT_SHADOWS");

        // TODO: Generate a matrix that transforms between 0-1 instead
        // of doing the extra math on the GPU
        Vector4 size = Vector4.zero;
        size.y = _shadowCam.orthographicSize * 2;
        size.x = _shadowCam.aspect * size.y;
        size.z = _shadowCam.farClipPlane;
        size.w = 1.0f /(int) _resolution;
        Shader.SetGlobalVector("_ShadowTexScale", size);
    }

    RenderTexture CreateTarget()
    {
        RenderTexture tg = new RenderTexture((int)_resolution, (int)_resolution, 24, RenderTextureFormat.RGFloat);
        tg.filterMode = _filterMode;
        tg.wrapMode = TextureWrapMode.Clamp;
        tg.enableRandomWrite = transform;
        tg.Create();

        return tg;
    }
    void GetRenderersExtents(List<Renderer> renderers, Transform frame, out Vector3 center, out Vector3 extents)
    {
        Vector3[] arr = new Vector3[8];

        Vector3 min = Vector3.one * Mathf.Infinity;
        Vector3 max = Vector3.one * Mathf.NegativeInfinity;
        foreach (var r in renderers)
        {
            GetBoundsPoints(r.bounds, arr, frame.worldToLocalMatrix);

            foreach (var p in arr)
            {
                for (int i = 0; i < 3; i++)
                {
                    min[i] = Mathf.Min(p[i], min[i]);
                    max[i] = Mathf.Max(p[i], max[i]);
                }
            }
        }

        extents = max - min;
        center = (max + min) / 2;
    }
    void GetBoundsPoints(Bounds b, Vector3[] points, Matrix4x4? mat = null)
    {
        Matrix4x4 trans = mat ?? Matrix4x4.identity;

        int count = 0;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 v = b.extents;
                    v.x *= x;
                    v.y *= y;
                    v.z *= z;
                    v += b.center;
                    v = trans.MultiplyPoint(v);

                    points[count++] = v;
                }
    }

    // Swap Elements A and B
    void Swap<T>(ref T a, ref T b)
    {
        T temp = a;
        a = b;
        b = temp;
    }

    void ForAllKeywords(System.Action<ShadowsType> func)
    {
        func(ShadowsType.HARD);
        func(ShadowsType.PCF);
        func(ShadowsType.VARIANCE);
        func(ShadowsType.MOMENT);
    }

    string ToKeyword(ShadowsType shadowType)
    {
        if (shadowType == ShadowsType.HARD) return "HARD_SHADOWS";
        if (shadowType == ShadowsType.PCF) return "PCF_SHADOWS";
        if (shadowType == ShadowsType.VARIANCE) return "VARIANCE_SHADOWS";
        if (shadowType == ShadowsType.MOMENT) return "MOMENT_SHADOWS";
        return "";
    }
}
