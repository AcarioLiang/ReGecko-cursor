using UnityEngine;

public class FrameRateController : MonoBehaviour
{
    public int targetFrameRate = 60; // 默认设置为60帧

    [Header("传感器设置")]
    public bool enableAccelerometer = false;
    public bool enableGyroscope = false;

    void Start()
    {
        // 确保VSync关闭，否则可能会忽略targetFrameRate的设置
        QualitySettings.vSyncCount = 0; // 设置 VSync Count 为 Don't Sync:cite[6]

        // 设置目标帧率
        Application.targetFrameRate = targetFrameRate;

        InitializeSensors();
        ApplyPerformanceSettings();
    }

    // 可以在运行时根据需要动态调整帧率
    public void SetFrameRate(int frameRate)
    {
        Application.targetFrameRate = frameRate;
    }

    void InitializeSensors()
    {
        // 加速度计控制 - 实际上无法直接设置频率，但可以禁用
        if (!enableAccelerometer)
        {
            // 通过不访问Input.acceleration来间接"禁用"
            // Unity会自动管理传感器生命周期
            Debug.Log("加速度计已禁用（不访问相关API）");
        }
        else
        {
            // 如果需要使用加速度计，只需访问即可
            Vector3 accel = Input.acceleration;
            Debug.Log("加速度计已启用");
        }

        // 陀螺仪控制
        if (SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = enableGyroscope;
            Debug.Log(enableGyroscope ? "陀螺仪已启用" : "陀螺仪已禁用");
        }
    }

    void ApplyPerformanceSettings()
    {
        // 设置帧率
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0;

        // 其他性能优化
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}