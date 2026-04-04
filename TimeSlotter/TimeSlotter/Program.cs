using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using TimeSlotter.Models;
using TimeSlotter.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var identityBuilder = builder.Services.AddIdentityCore<Provider>(options =>
{
    // UX for students / demo: allow short passwords but still require 6+ length.
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
});

identityBuilder.AddRoles<IdentityRole<int>>();
identityBuilder.AddSignInManager();
identityBuilder.AddEntityFrameworkStores<AppDbContext>();

// Cookie auth: default scheme must be Identity's application cookie scheme.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/Login";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Required order for Razor Pages + Identity: routing → authenticate → authorize → endpoints.
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
