# SAGARA: Stories of the Island

**SAGARA** adalah game *cozy 3D life simulation* tentang kehidupan di **Pulau Lembayung**, sebuah pulau fiktif di Indonesia timur yang sedang meredup. Pemain kembali ke pulau asal keluarganya, berkebun, memancing, membantu warga, dan memulihkan lingkungan pesisir sampai **Festival Bahari** bisa digelar lagi.

> Proyek mata kuliah **Grafika Komputer** — dikerjakan oleh tim beranggotakan 3 orang.

---

## Konsep

| | |
| --- | --- |
| Genre | Cozy life simulation / adventure |
| Platform | PC (Windows), single-player |
| Kamera | 3D third-person, sudut 3/4 dari atas (orthographic) |
| Gaya visual | Stylized low poly, chibi, warna pastel |
| Tema | SDG 14 (*Life Below Water*) dan SDG 17 (*Partnerships for the Goals*) |
| Referensi | Story of Seasons, Animal Crossing, Stardew Valley, Coral Island |

**Cerita singkat:** dulu Pulau Lembayung punya pantai indah dan perikanan yang ramai. Kini dermaganya rusak, pesisirnya kotor, dan terumbu karangnya rusak. Pemain bukan pahlawan tunggal — pulau pulih lewat kerja sama warga.

### Progres pulau

| Level | Nama | Yang terbuka |
| --- | --- | --- |
| 0 | A New Life | Rumah & kebun kecil, memancing di pantai, kenalan dengan warga |
| 1 | Coastal Expansion | Dermaga, perahu, area memancing baru |
| 2 | Nature Discovery | Area mangrove dan penanaman mangrove |
| 3 | The Coral Sanctuary | Restorasi terumbu karang |
| 4 | Island Revival | Festival Bahari dan fasilitas desa terakhir |

### Loop permainan

**Jelajahi → Interaksi (berkebun, memancing, bicara dengan warga) → Kumpulkan (barang, uang, Community Points) → Buka area & fasilitas baru.**

---

## Status pengembangan

| Fitur | Status |
| --- | --- |
| Gerak Player + animasi + kamera follow | ✅ Bisa dimainkan |
| Area rumah & kebun | ✅ Bisa dimainkan |
| Farming (tanam, siram, tumbuh, panen) | ✅ Dasar sudah jalan |
| Membersihkan sampah pesisir | ✅ Dasar sudah jalan |
| Desa utama: Balai Desa & alun-alun | ✅ Environment sementara |
| Dermaga | 🚧 Dikerjakan |
| Fishing, UI inventory, dialog NPC | 🚧 Dikerjakan |
| Perpindahan area, save/load | ⏳ Direncanakan |
| Pasar, toko alat, rumah warga, area festival | ⏳ Setelah tiga area utama selesai |

Asset lingkungan saat ini sebagian besar **sementara**; tim memprioritaskan **logic gameplay** lebih dulu.

---

## Kontrol

| Tombol | Aksi |
| --- | --- |
| `W` `A` `S` `D` | Berjalan |
| `E` | Interaksi (bertani, mengambil sampah) |
| Scroll mouse | Zoom kamera |

---

## Teknologi

- **Unity 6000.3.27f1** (Unity 6.3 LTS) — wajib versi yang sama, lihat `ProjectSettings/ProjectVersion.txt`
- **Universal Render Pipeline (URP)** 17.3
- **Input System** 1.20
- **Git LFS** untuk `.fbx`, `.blend`, `.psd`, `.wav`, `.mp4`
- Model dibuat/diolah di **Blender**, diekspor sebagai FBX

---

## Cara menjalankan project

1. Pasang **Unity 6000.3.27f1** lewat Unity Hub.
2. Pasang **Git** dan **Git LFS**, lalu jalankan sekali:
   ```bash
   git lfs install
   ```
3. Clone repository dan unduh file LFS:
   ```bash
   git clone https://github.com/DapaAyayyy/SAGARA-Stories-of-the-Island.git
   cd SAGARA-Stories-of-the-Island
   git lfs pull
   ```
4. Buka folder project di Unity Hub, tunggu proses import selesai.
5. Buka salah satu scene lalu tekan **Play**:
   - `Assets/SAGARA/Scenes/RumahDanKebun.unity` — area rumah & kebun
   - `Assets/SAGARA/Scenes/MainVillage.unity` — desa utama, Balai Desa, dan alun-alun

---

## Struktur folder

```text
Assets/SAGARA/
├── Art/            Model, material, tekstur, animasi, terrain
├── Data/           Data gameplay (mis. definisi tanaman)
├── Prefabs/        Prefab lingkungan, farming, dan desa (Village/)
├── Scenes/         RumahDanKebun, MainVillage
├── Scripts/
│   ├── Player/     Gerak, kamera, interaksi pemain
│   ├── Farming/    Petak kebun, pertumbuhan tanaman, inventory kebun, HUD
│   ├── Cleanup/    Sampah pesisir dan inventory sampah
│   └── Editor/     Alat bantu editor (menu SAGARA) untuk menyusun desa
├── SagaraFishing/  Aset referensi pulau untuk fishing
└── UI/             Antarmuka
ArtSource/          File sumber Blender (.blend)
```

**Menu editor SAGARA** (Unity → menu bar **SAGARA**): *Greybox Balai Desa*, *Balai Desa Sementara (MegaKit)*, dan *Lengkapi Area Balai Desa* menyusun ulang area desa secara otomatis kalau ukurannya berubah. Script ini hanya berjalan di Editor dan tidak ikut ke build.

---

## Tim dan pembagian kerja

| Peran | Fokus |
| --- | --- |
| Anggota 1 | Main village, Balai Desa & alun-alun: layout, bangunan, collider, lighting, titik interaksi |
| Anggota 2 | Area rumah & kebun, Player, farming, inventory, save/load, perpindahan area |
| Anggota 3 | Fishing, UI/HUD, dialog NPC |

### Alur kerja Git

1. Buat branch fitur dari `main` terbaru:
   ```bash
   git switch main
   git pull --ff-only
   git switch -c feat/nama-fitur
   ```
2. Commit scene, prefab, script, **beserta file `.meta`**-nya. Jangan ikutkan perubahan `ProjectSettings/` yang tidak disengaja.
3. Push lalu buka **Pull Request** ke `main`; minta review sebelum merge.

---

## Kredit asset

Asset pihak ketiga yang dipakai (sementara):

| Asset | Pembuat | Lisensi |
| --- | --- | --- |
| [Medieval Village MegaKit](https://quaternius.com/packs/medievalvillagemegakit.html) (versi gratis) | Quaternius | CC0 |
| Stylized Nature MegaKit (versi gratis) | [Quaternius](https://quaternius.com) | CC0 |
| ToonTropical | — | Sedang dikonfirmasi |
| Model karakter | Dibuat dengan Meshy AI | — |
| Animasi karakter | Mixamo (Adobe) | Lisensi Mixamo |

Props desa, material warna, tata letak, dan script gameplay dibuat oleh Tim SAGARA.

---

*SAGARA: Stories of the Island — Hidupkan Pulau Lembayung, gelar kembali Festival Bahari.* 🌊
