using EnergyApp.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyApp.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260405121000_AddPostalIncomingFields")]
    partial class AddPostalIncomingFields
    {
    }
}
