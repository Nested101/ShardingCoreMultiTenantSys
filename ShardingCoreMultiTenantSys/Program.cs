using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShardingCore;
using ShardingCore.Bootstrappers;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources.Common;
using ShardingCoreMultiTenantSys.DbContexts;
using ShardingCoreMultiTenantSys.Extensions;
using ShardingCoreMultiTenantSys.IdentitySys;
using ShardingCoreMultiTenantSys.Middlewares;
using ShardingCoreMultiTenantSys.TenantSys.Shardings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddAuthentication();
#region �û�ϵͳ����

builder.Services.AddDbContext<IdentityDbContext>(o =>
    o.UseSqlServer("Data Source=localhost;Initial Catalog=IdDb;Integrated Security=True;"));
//������Կ
var keyByteArray = Encoding.ASCII.GetBytes("123123!@#!@#123123");
var signingKey = new SymmetricSecurityKey(keyByteArray);
//��֤����
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = "https://localhost:5000",
            ValidateAudience = true,
            ValidAudience = "api",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true,
        };
    });
#endregion
#region ����ShardingCore
builder.Services.AddShardingDbContext<TenantDbContext>()
    .AddEntityConfig(op =>
    {
        op.CreateShardingTableOnStart = true;
        op.EnsureCreatedWithOutShardingTable = true;
        op.AddShardingTableRoute<OrderVirtualTableRoute>();
    })
    .AddConfig(op =>
    {
        //Ĭ������һ��
        op.ConfigId = $"test_{Guid.NewGuid():n}";
        op.Priority = 99999;
        op.AddDefaultDataSource("ds0", "Data Source=localhost;Initial Catalog=TestTenantDb;Integrated Security=True;");
        op.UseShardingQuery((conStr, b) =>
        {
            b.UseSqlServer(conStr);
        });
        op.UseShardingTransaction((conn, b) =>
        {
            b.UseSqlServer(conn);
        });
    }).EnsureMultiConfig(ShardingConfigurationStrategyEnum.ThrowIfNull);

#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
app.Services.GetRequiredService<IShardingBootstrapper>().Start();
//��ʼ�����������⻧��Ϣ
app.Services.InitTenant();
app.UseAuthorization();

//����֤�������⻧ѡ���м��
app.UseMiddleware<TenantSelectMiddleware>();

app.MapControllers();

app.Run();
