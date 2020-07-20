using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[ExecuteAlways]
public class ShadowMapping : MonoBehaviour
{
    private Camera LightCamera;
    public Shader ligthCameraShader;
    public Light dirLight;
    [Range(0,1)]
    public float ShadowStrength = 0.5f;
    [Range(0,2)]
    public float ShadowBias = 0.005f;

    private string dirLightName = "Dir Light Camera";
    [SerializeField]
    public enum CustomShadowResolution { 
        Low=1,
        Middle=2,
        High=4,
        VeryHigh=8,
    }
    public CustomShadowResolution shadowResolution =  CustomShadowResolution.Low;
    void Start()
    {
        LightCamera = CreateDirLightCamera();
        if (!LightCamera.targetTexture)
            LightCamera.targetTexture = Create2DTexture(LightCamera);
    }

    void Update()
    {
        if(LightCamera==null)
            LightCamera = CreateDirLightCamera();

        LightCamera.transform.position = dirLight.gameObject.transform.position;
        LightCamera.transform.rotation = dirLight.gameObject.transform.rotation;
        LightCamera.transform.localScale = dirLight.gameObject.transform.localScale;

        Shader.SetGlobalFloat("_gShadowBias", ShadowBias);

        Matrix4x4 PMatrix = GL.GetGPUProjectionMatrix(LightCamera.projectionMatrix, false);  //处理不同平台投影矩阵的差异性
        Shader.SetGlobalMatrix("_gWorldToShadow",  PMatrix*LightCamera.worldToCameraMatrix); //当前片段从世界坐标转换到光源空间坐标
        Shader.SetGlobalFloat("_gShadowStrength", ShadowStrength);
        LightCamera.RenderWithShader(ligthCameraShader, ""); //设置深度相机绘制shader为自定义shader
    }
    public Camera CreateDirLightCamera()
    {
        Camera lightCamera=null;
        var goLightCamera = GameObject.Find(dirLightName);
        if (goLightCamera != null)
        {
            lightCamera = goLightCamera.GetComponent<Camera>();
            ResetCaneraArgs(ref lightCamera);
        }
        else
        {
            goLightCamera = new GameObject(dirLightName);// 光源处相机
            lightCamera = goLightCamera.AddComponent<Camera>();
            ResetCaneraArgs(ref lightCamera);
        }
        return lightCamera;
    }

    private void ResetCaneraArgs(ref Camera lightCamera)
    {
        lightCamera.backgroundColor = Color.white;
        lightCamera.clearFlags = CameraClearFlags.SolidColor;
        lightCamera.orthographic = true;
        lightCamera.orthographicSize = 6f;
        lightCamera.nearClipPlane = 0.3f;
        lightCamera.farClipPlane = 20f;
        lightCamera.enabled = false;
        lightCamera.allowMSAA = false;
        lightCamera.allowHDR = false;
        lightCamera.cullingMask = 1 << LayerMask.NameToLayer("Caster");  //设置CullingMask为"Caster"
    }

    private RenderTexture Create2DTexture(Camera cam)
    {
        RenderTextureFormat rtFormat = RenderTextureFormat.Default;
        if (!SystemInfo.SupportsRenderTextureFormat(rtFormat))
            rtFormat = RenderTextureFormat.Default;
        var rt_2d = new RenderTexture(512 * (int)shadowResolution, 512 * (int)shadowResolution, 24, rtFormat);
        rt_2d.hideFlags = HideFlags.DontSave;
        Shader.SetGlobalTexture("_gShadowMapTexture", rt_2d);

        return rt_2d;
    }
}
