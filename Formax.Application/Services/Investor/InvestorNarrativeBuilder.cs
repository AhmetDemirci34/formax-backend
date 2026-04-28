namespace Formax.Application.Services.Investor
{
    public static class InvestorNarrativeBuilder
    {
        public static string BuildOverview(
            int totalDecisions,
            int extended,
            int silent,
            int shortState,
            int selfRetracted)
        {
            return
$@"Son 30 günlük veriye göre FORMAX AI toplam {totalDecisions} karar üretmiştir.
Bu kararların {extended} tanesinde AI kapsamlı analiz sunmayı tercih etmiş, {silent} durumda ise bilinçli olarak susmuştur.
Kısa cevap verilen durum sayısı {shortState} olup, sistem bu periyotta kendini geri çekmesini gerektiren çelişkili bir sinyal algılamamıştır.";
        }

        public static string BuildWhyItMatters(
            int extended,
            int silent)
        {
            return
extended > silent
    ? "Bu dağılım, FORMAX AI’ın yüksek güven oluştuğunda konuşmayı, belirsizlik durumunda ise kullanıcıyı koruyarak susmayı tercih eden dengeli bir davranış modeline sahip olduğunu göstermektedir."
    : "Bu dağılım, FORMAX AI’ın belirsizlik durumlarında konuşmak yerine susmayı tercih eden, riskten kaçınan bir karar destek yaklaşımı benimsediğini göstermektedir.";
        }

        public static string BuildWhyFormax()
        {
            return
"FORMAX, tahmin satan veya yönlendiren sistemlerin aksine, kararın her zaman kullanıcıda kaldığı; AI’ın ise yalnızca veri, bağlam ve senaryo sunduğu etik ve sürdürülebilir bir futbol karar destek platformudur.";
        }
    }
}
