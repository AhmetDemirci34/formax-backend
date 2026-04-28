namespace Formax.Application.Interfaces
{
    public interface ICouponItemUpdateRepository
    {
        void SetResultByMatch(int matchId, bool isSuccess);
    }
}
