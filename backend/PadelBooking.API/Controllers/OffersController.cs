using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PadelBooking.API.Data;
using PadelBooking.API.DTOs;
using PadelBooking.API.Models;

namespace PadelBooking.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class OffersController : ControllerBase
{
    private readonly AppDbContext _context;

    public OffersController(AppDbContext context)
    {
        _context = context;
    }

    // Returns all offers
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Offer>>> GetOffers(
        CancellationToken cancellationToken)
    {
        var offers = await _context.Offers
            .OrderBy(offer => offer.MinimumHours)
            .ToListAsync(cancellationToken);

        return Ok(offers);
    }

    // Returns active offer information that customers may view safely.
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<PublicOfferDto>>> GetPublicOffers(
        CancellationToken cancellationToken)
    {
        // Decimal aggregation is performed in memory for SQLite compatibility.
        var activeCourtPrices = await _context.Courts
            .AsNoTracking()
            .Where(court => court.IsActive)
            .Select(court => court.PricePerHour)
            .ToListAsync(cancellationToken);
        var standardPricePerHour = activeCourtPrices.Count > 0
            ? activeCourtPrices.Min()
            : (decimal?)null;

        var offers = await _context.Offers
            .AsNoTracking()
            .Where(offer => offer.IsActive)
            .OrderBy(offer => offer.MinimumHours)
            .ToListAsync(cancellationToken);

        var result = offers.Select(offer =>
        {
            var effectivePricePerHour = standardPricePerHour.HasValue
                ? Math.Min(standardPricePerHour.Value, offer.PricePerHour)
                : offer.PricePerHour;
            var originalTotalPrice = standardPricePerHour * offer.MinimumHours;
            var offerTotalPrice = effectivePricePerHour * offer.MinimumHours;

            return new PublicOfferDto
            {
                Id = offer.Id,
                MinimumHours = offer.MinimumHours,
                PricePerHour = effectivePricePerHour,
                StandardPricePerHour = standardPricePerHour,
                OriginalTotalPrice = originalTotalPrice,
                OfferTotalPrice = offerTotalPrice,
                Savings = originalTotalPrice - offerTotalPrice
            };
        }).ToList();

        return Ok(result);
    }

    // Returns a specific offer by ID
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Offer>> GetOffer(int id)
    {
        var offer = await _context.Offers.FindAsync(id);

        if (offer == null)
        {
            return NotFound(new
            {
                message = "Offer not found."
            });
        }

        return Ok(offer);
    }

    // Creates a new offer
    [HttpPost]
    public async Task<ActionResult<Offer>> CreateOffer(UpsertOfferDto dto)
    {
        var duplicateOffer = await _context.Offers.AnyAsync(
            existingOffer =>
                existingOffer.MinimumHours == dto.MinimumHours);

        if (duplicateOffer)
        {
            return Conflict(new
            {
                message = "An offer with the same minimum hours already exists."
            });
        }

        var offer = new Offer
        {
            MinimumHours = dto.MinimumHours,
            PricePerHour = dto.PricePerHour,
            IsActive = dto.IsActive
        };

        _context.Offers.Add(offer);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetOffer),
            new { id = offer.Id },
            offer);
    }

    // Updates an existing offer
    [HttpPut("{id}")]
    public async Task<ActionResult<Offer>> UpdateOffer(int id, UpsertOfferDto dto)
    {
        var existingOffer = await _context.Offers.FindAsync(id);

        if (existingOffer == null)
        {
            return NotFound(new
            {
                message = "Offer not found."
            });
        }

        var duplicateOffer = await _context.Offers.AnyAsync(
            otherOffer =>
                otherOffer.Id != id &&
                otherOffer.MinimumHours == dto.MinimumHours);

        if (duplicateOffer)
        {
            return Conflict(new
            {
                message = "Another offer with the same minimum hours already exists."
            });
        }

        existingOffer.MinimumHours = dto.MinimumHours;
        existingOffer.PricePerHour = dto.PricePerHour;
        existingOffer.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return Ok(existingOffer);
    }

    // Deletes an offer
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOffer(int id)
    {
        var offer = await _context.Offers.FindAsync(id);

        if (offer == null)
        {
            return NotFound(new
            {
                message = "Offer not found."
            });
        }

        _context.Offers.Remove(offer);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Offer deleted successfully."
        });
    }
}
