using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RoomRum.API.Controllers;
using RoomRum.BLL.DTOs.Responses;
using RoomRum.BLL.Services.Implementations;
using RoomRum.DAL.Entities;
using RoomRum.DAL.Repositories.Implementations;
using Xunit;

public sealed class RoomManagementTests
{
    [Theory]
    [InlineData("Pending")]
    [InlineData("Available")]
    [InlineData("Rented")]
    [InlineData("Cancelled")]
    [InlineData("Hidden")]
    public async Task OwnerAndAdminCanReadFullDetailsButOtherOwnersCannot(string status)
    {
        await using var db = CreateDb();
        var room = await Seed(db, status);
        var service = new RoomService(new RoomRepository(db), new AmenityRepository(db));
        foreach (var (viewer, admin) in new[] { (room.UserId, false), (Guid.NewGuid(), true) })
        {
            var result = await service.ManagementDetail(room.RoomId, viewer, admin);
            Assert.Equal(200, result.StatusCode);
            var detail = Assert.IsType<RoomResponse>(result.Data);
            Assert.Equal("Full description", detail.Description);
            Assert.Equal("House rules", detail.Rule);
            Assert.Equal(500000m, detail.Deposit);
            Assert.Equal(3500m, detail.ElectricityFee);
            Assert.Equal("Owner", detail.Owner!.Name);
            Assert.Single(detail.Images!);
            Assert.Single(detail.Amenities!);
        }
        Assert.Equal(404, (await service.ManagementDetail(room.RoomId, Guid.NewGuid(), false)).StatusCode);
        Assert.Equal(404, (await service.ManagementDetail(Guid.NewGuid(), room.UserId, false)).StatusCode);
        var mine = Assert.IsType<List<RoomMineResponse>>((await service.Mine(room.UserId, 1, 50)).Data);
        Assert.Single(mine);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Cancelled")]
    [InlineData("Hidden")]
    public async Task UnpublishedRoomsStayUnavailableOnPublicEndpoint(string status)
    {
        await using var db = CreateDb(); var room = await Seed(db, status);
        var service = new RoomService(new RoomRepository(db), new AmenityRepository(db));
        Assert.Equal(404, (await service.Detail(room.RoomId, null)).StatusCode);
        Assert.Equal(404, (await service.Detail(room.RoomId, room.UserId)).StatusCode);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Hidden")]
    [InlineData("Cancelled")]
    public async Task PrivateImageContentRequiresOwnerOrAdmin(string status)
    {
        await using var db = CreateDb(); var room = await Seed(db, status);
        var repository = new RoomRepository(db);
        var imageId = room.RoomImages.Single().RoomImageId;
        var directory = Path.Combine(Path.GetTempPath(), "roomrum-tests-" + Guid.NewGuid());
        var imageDirectory = Path.Combine(directory, "App_Data", "room-images", room.RoomId.ToString("N"));
        Directory.CreateDirectory(imageDirectory);
        await File.WriteAllBytesAsync(Path.Combine(imageDirectory, imageId.ToString("N") + ".png"), [137, 80, 78, 71]);
        try
        {
            var controller = new RoomImagesController(new RoomImageService(repository), new TestEnvironment(directory));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            Assert.IsType<NotFoundResult>(await controller.Content(room.RoomId, imageId));
            controller.HttpContext.User = Identity(Guid.NewGuid(), "Landlord");
            Assert.IsType<NotFoundResult>(await controller.Content(room.RoomId, imageId));
            controller.HttpContext.User = Identity(room.UserId, "Landlord");
            Assert.IsType<PhysicalFileResult>(await controller.Content(room.RoomId, imageId));
            controller.HttpContext.User = Identity(Guid.NewGuid(), "Admin");
            Assert.IsType<PhysicalFileResult>(await controller.Content(room.RoomId, imageId));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void ManagementEndpointRequiresLandlordOrAdmin()
    {
        var method = typeof(RoomsController).GetMethod(nameof(RoomsController.ManagementDetail))!;
        var authorization = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Landlord,Admin", authorization.Roles);
        Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    private static ClaimsPrincipal Identity(Guid id, string role) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)], "Test"));
    private static RoomRumDbContext CreateDb() => new(new DbContextOptionsBuilder<RoomRumDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<Room> Seed(RoomRumDbContext db, string status)
    {
        var owner = new User { UserId = Guid.NewGuid(), Name = "Owner", Password = "test", Status = "Active",
            Role = new Role { RoleId = Guid.NewGuid(), RoleName = "Landlord" } };
        var room = new Room { RoomId = Guid.NewGuid(), User = owner, UserId = owner.UserId,
            Title = "Room", Status = status, Description = "Full description", Rule = "House rules",
            Deposit = 500000m, ElectricityFee = 3500m };
        room.Amenities.Add(new Amenity { AmenityId = Guid.NewGuid(), AmenityName = "Wifi" });
        var imageId = Guid.NewGuid();
        room.RoomImages.Add(new RoomImage { RoomImageId = imageId, RoomId = room.RoomId,
            ImageUrl = $"/api/rooms/{room.RoomId}/images/{imageId}/content" });
        db.Rooms.Add(room); await db.SaveChangesAsync(); return room;
    }
    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
