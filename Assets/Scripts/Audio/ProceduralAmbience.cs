using UnityEngine;

namespace LastLight.Audio
{
    /// <summary>
    /// Ruzgar ve ates seslerini calisma aninda uretir.
    ///
    /// Bu ikisi icin hazir dosya indirmedik cunku her ikisi de gurultu
    /// tabanli: gercekte de filtrelenmis gurultuden yapiliyorlar. Sentez
    /// hem dosya tasimayi hem lisans takibini ortadan kaldiriyor, hem de
    /// dongude duyulan "ayni desen tekrar ediyor" hissini azaltiyor cunku
    /// uretilen parca istedigimiz kadar uzun olabiliyor.
    ///
    /// Donguyu dikissiz yapmak icin sonun bir saniyesi basa capraz
    /// karistiriliyor; yoksa her tur basinda duyulur bir "tik" oluyor.
    /// </summary>
    public static class ProceduralAmbience
    {
        const int Rate = 44100;

        // ---------- Ruzgar ----------

        public static AudioClip Ruzgar(float saniye = 14f, int tohum = 1337)
        {
            int fade = Rate;                       // 1 saniyelik capraz gecis
            int ham = Mathf.RoundToInt(Rate * saniye) + fade;
            var data = new float[ham];

            var rnd = new System.Random(tohum);
            float kahve = 0f, alcak = 0f;

            for (int i = 0; i < ham; i++)
            {
                float beyaz = (float)(rnd.NextDouble() * 2.0 - 1.0);

                // Kahverengi gurultu: beyaz gurultunun sizintili integrali.
                // Ruzgarin enerjisi dusuk frekanslarda toplandigi icin duz
                // beyaz gurultu "hisirti", kahverengi ise "ugultu" veriyor.
                kahve = 0.996f * kahve + 0.004f * beyaz;

                float t = i / (float)Rate;

                // Iki yavas ve birbirine bolunmeyen sinus: dogal esme.
                // Tek sinus kullanirsak nefes alip veren bir makine gibi
                // duyuluyor, periyot hemen fark ediliyor.
                float esinti = 0.55f + 0.45f *
                    (0.62f * Mathf.Sin(t * 0.17f * Mathf.PI * 2f) +
                     0.38f * Mathf.Sin(t * 0.061f * Mathf.PI * 2f + 1.3f));
                esinti = Mathf.Clamp01(esinti);

                // Esme siddetlendikce filtre aciliyor: guclu ruzgar daha tiz.
                float kesim = Mathf.Lerp(0.004f, 0.045f, esinti);
                alcak += kesim * (kahve * 9f + beyaz * 0.30f - alcak);

                data[i] = alcak * esinti;
            }

            return Bitir("Ruzgar", data, fade, 0.55f);
        }

        // ---------- Ates ----------

        public static AudioClip Ates(float saniye = 8f, int tohum = 7331)
        {
            int fade = Rate / 2;
            int ham = Mathf.RoundToInt(Rate * saniye) + fade;
            var data = new float[ham];

            var rnd = new System.Random(tohum);
            float kahve = 0f, alcak = 0f;

            // Taban: sicak, bogumsuz bir ugultu.
            for (int i = 0; i < ham; i++)
            {
                float beyaz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                kahve = 0.993f * kahve + 0.007f * beyaz;
                alcak += 0.02f * (kahve * 7f - alcak);
                data[i] = alcak * 0.5f;
            }

            // Catirti: kisa, hizli sonen gurultu patlamalari. Atesi ates yapan
            // sey surekli ugultu degil bu duzensiz patlamalar.
            int yer = 0;
            while (yer < ham)
            {
                yer += rnd.Next(Rate / 25, Rate / 3);       // 40 ms - 330 ms arasi
                if (yer >= ham) break;

                int uzunluk = rnd.Next(Rate / 400, Rate / 60);   // 2.5 ms - 16 ms
                float guc = 0.25f + (float)rnd.NextDouble() * 0.75f;
                float sonum = 6f + (float)rnd.NextDouble() * 20f;

                for (int j = 0; j < uzunluk && yer + j < ham; j++)
                {
                    float t = j / (float)uzunluk;
                    float zarf = Mathf.Exp(-sonum * t);
                    float beyaz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    data[yer + j] += beyaz * zarf * guc * 0.6f;
                }
            }

            return Bitir("Ates", data, fade, 0.7f);
        }

        // ---------- Ortak ----------

        static AudioClip Bitir(string ad, float[] data, int fade, float hedefTepe)
        {
            int n = data.Length - fade;

            // Sonun fade kadarlik kismini basa karistir: dongu dikissiz olsun.
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = data[i] * a + data[n + i] * (1f - a);
            }

            // Tepe degere gore olcekle. Sabit bir carpan kullanmak, tohum
            // degisince sesin bir kisilip bir patlamasina yol aciyordu.
            float tepe = 0.0001f;
            for (int i = 0; i < n; i++) tepe = Mathf.Max(tepe, Mathf.Abs(data[i]));

            float olcek = hedefTepe / tepe;
            var son = new float[n];
            for (int i = 0; i < n; i++) son[i] = data[i] * olcek;

            var clip = AudioClip.Create(ad, n, 1, Rate, false);
            clip.SetData(son, 0);
            return clip;
        }
    }
}
