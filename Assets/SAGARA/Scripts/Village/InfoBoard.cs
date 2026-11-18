using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dipasang di titik interaksi yang bisa "dibaca" dengan tombol E: papan informasi,
/// papan petunjuk arah, papan penghalang dermaga, dan sejenisnya.
/// Menyimpan tulisan petunjuk, judul, dan isi yang ditampilkan saat Player membacanya.
/// </summary>
[DisallowMultipleComponent]
public sealed class InfoBoard : MonoBehaviour
{
    // Daftar semua papan aktif, supaya Player bisa mencari papan terdekat tanpa Physics.
    private static readonly HashSet<InfoBoard> activeBoards = new HashSet<InfoBoard>();

    [SerializeField] private string prompt = "Tekan [E] untuk membaca Papan Informasi";
    [SerializeField] private string title = "Papan Informasi Desa Lembayung";
    [SerializeField, TextArea(2, 4)] private string[] entries =
    {
        "Selamat datang di Pulau Lembayung! Balai Desa buka setiap hari untuk membantu warga.",
        "Festival Bahari akan digelar kembali setelah desa dan pesisir pulih. Ayo bantu warga!",
        "Pesisir kotor: kumpulkan sampah di pantai dan buang ke tempat sampah organik/anorganik.",
        "Dermaga akan dibuka setelah warga mengumpulkan cukup Community Points.",
    };
    [SerializeField, Min(0.5f)] private float readDistance = 2f;

    public static IEnumerable<InfoBoard> ActiveBoards => activeBoards;
    public string Prompt => prompt;
    public string Title => title;
    public float ReadDistance => readDistance;
    public int EntryCount => entries == null ? 0 : entries.Length;

    public string GetEntry(int index)
    {
        return index >= 0 && index < EntryCount ? entries[index] : string.Empty;
    }

    private void OnEnable()
    {
        activeBoards.Add(this);
    }

    private void OnDisable()
    {
        activeBoards.Remove(this);
    }

    private void OnDrawGizmosSelected()
    {
        // Lingkaran kuning di Scene = jarak Player bisa membaca papan.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, readDistance);
    }
}
