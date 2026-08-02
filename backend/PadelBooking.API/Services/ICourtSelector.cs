using System.Security.Cryptography;
using PadelBooking.API.Models;

namespace PadelBooking.API.Services;

public interface ICourtSelector
{
    Court Select(IReadOnlyList<Court> availableCourts);
}

public sealed class RandomCourtSelector : ICourtSelector
{
    public Court Select(IReadOnlyList<Court> availableCourts)
    {
        if (availableCourts.Count == 0)
        {
            throw new ArgumentException("At least one available court is required.", nameof(availableCourts));
        }

        return availableCourts[RandomNumberGenerator.GetInt32(availableCourts.Count)];
    }
}
