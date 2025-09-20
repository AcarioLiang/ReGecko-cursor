using UnityEngine;

public class FrameRateController : MonoBehaviour
{
    public int targetFrameRate = 60; // Ĭ������Ϊ60֡

    [Header("����������")]
    public bool enableAccelerometer = false;
    public bool enableGyroscope = false;

    void Start()
    {
        // ȷ��VSync�رգ�������ܻ����targetFrameRate������
        QualitySettings.vSyncCount = 0; // ���� VSync Count Ϊ Don't Sync:cite[6]

        // ����Ŀ��֡��
        Application.targetFrameRate = targetFrameRate;

        InitializeSensors();
        ApplyPerformanceSettings();
    }

    // ����������ʱ������Ҫ��̬����֡��
    public void SetFrameRate(int frameRate)
    {
        Application.targetFrameRate = frameRate;
    }

    void InitializeSensors()
    {
        // ���ٶȼƿ��� - ʵ�����޷�ֱ������Ƶ�ʣ������Խ���
        if (!enableAccelerometer)
        {
            // ͨ��������Input.acceleration�����"����"
            // Unity���Զ�����������������
            Debug.Log("���ٶȼ��ѽ��ã����������API��");
        }
        else
        {
            // �����Ҫʹ�ü��ٶȼƣ�ֻ����ʼ���
            Vector3 accel = Input.acceleration;
            Debug.Log("���ٶȼ�������");
        }

        // �����ǿ���
        if (SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = enableGyroscope;
            Debug.Log(enableGyroscope ? "������������" : "�������ѽ���");
        }
    }

    void ApplyPerformanceSettings()
    {
        // ����֡��
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0;

        // ���������Ż�
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}