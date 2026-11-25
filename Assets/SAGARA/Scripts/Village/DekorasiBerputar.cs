using UnityEngine;

/// <summary>
/// Dipasang di hiasan festival (misalnya bintang besar di tiang bendera).
/// Membuat hiasan berputar pelan dan naik-turun, supaya suasana festival terasa hidup.
/// </summary>
[DisallowMultipleComponent]
public sealed class DekorasiBerputar : MonoBehaviour
{
    [SerializeField] private float kecepatanPutar = 40f;   // derajat per detik (0 = tidak berputar)
    [SerializeField] private float tinggiAyun = 0.15f;     // meter
    [SerializeField] private float kecepatanAyun = 1.5f;

    private Vector3 posisiAwal;
    private float jeda;

    private void Awake()
    {
        posisiAwal = transform.localPosition;
        jeda = Random.value * 10f; // supaya hiasan tidak bergerak serempak
    }

    private void Update()
    {
        if (kecepatanPutar != 0f)
        {
            transform.Rotate(0f, kecepatanPutar * Time.deltaTime, 0f, Space.World);
        }

        float ayun = Mathf.Sin((Time.time + jeda) * kecepatanAyun) * tinggiAyun;
        transform.localPosition = posisiAwal + Vector3.up * ayun;
    }
}
