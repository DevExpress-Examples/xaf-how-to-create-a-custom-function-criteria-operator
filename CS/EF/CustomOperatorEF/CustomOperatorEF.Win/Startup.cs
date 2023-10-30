﻿using System.Configuration;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ApplicationBuilder;
using DevExpress.ExpressApp.Win.ApplicationBuilder;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Win;
using DevExpress.Persistent.Base;
using Microsoft.EntityFrameworkCore;
using DevExpress.ExpressApp.EFCore;
using DevExpress.EntityFrameworkCore.Security;
using DevExpress.XtraEditors;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using DevExpress.ExpressApp.Design;
using CustomOperatorEF.Module.BusinessObjects;
using DevExpress.Data.Filtering;

namespace CustomOperatorEF.Win;

public class ApplicationBuilder : IDesignTimeApplicationFactory {
    public static WinApplication BuildApplication(string connectionString) {
        var builder = WinApplication.CreateBuilder();
        // Register custom services for Dependency Injection. For more information, refer to the following topic: https://docs.devexpress.com/eXpressAppFramework/404430/
        // builder.Services.AddScoped<CustomService>();
        // Register 3rd-party IoC containers (like Autofac, Dryloc, etc.)
        // builder.UseServiceProviderFactory(new DryIocServiceProviderFactory());
        // builder.UseServiceProviderFactory(new AutofacServiceProviderFactory());

        builder.UseApplication<CustomOperatorEFWindowsFormsApplication>();
        builder.Modules
            .AddConditionalAppearance()
            .AddValidation(options => {
                options.AllowValidationDetailsAccess = false;
            })
            .Add<CustomOperatorEF.Module.CustomOperatorEFModule>()
        	.Add<CustomOperatorEFWinModule>();
        builder.ObjectSpaceProviders
            .AddSecuredEFCore(options => options.PreFetchReferenceProperties())
                .WithDbContext<CustomOperatorEF.Module.BusinessObjects.CustomOperatorEFEFCoreDbContext>((application, options) => {
                    // Uncomment this code to use an in-memory database. This database is recreated each time the server starts. With the in-memory database, you don't need to make a migration when the data model is changed.
                    // Do not use this code in production environment to avoid data loss.
                    // We recommend that you refer to the following help topic before you use an in-memory database: https://docs.microsoft.com/en-us/ef/core/testing/in-memory
                    //options.UseInMemoryDatabase("InMemory");
                    options.UseSqlServer(connectionString);
                    options.UseChangeTrackingProxies();
                    options.UseObjectSpaceLinkProxies();
                })
            .AddNonPersistent();
        builder.Security
            .UseIntegratedMode(options => {
                options.RoleType = typeof(PermissionPolicyRole);
                options.UserType = typeof(CustomOperatorEF.Module.BusinessObjects.ApplicationUser);
                options.UserLoginInfoType = typeof(CustomOperatorEF.Module.BusinessObjects.ApplicationUserLoginInfo);
                options.Events.OnSecurityStrategyCreated += securityStrategy => {
                   // Use the 'PermissionsReloadMode.NoCache' option to load the most recent permissions from the database once
                   // for every DbContext instance when secured data is accessed through this instance for the first time.
                   // Use the 'PermissionsReloadMode.CacheOnFirstAccess' option to reduce the number of database queries.
                   // In this case, permission requests are loaded and cached when secured data is accessed for the first time
                   // and used until the current user logs out. 
                   // See the following article for more details: https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.Security.SecurityStrategy.PermissionsReloadMode.
                   ((SecurityStrategy)securityStrategy).PermissionsReloadMode = PermissionsReloadMode.NoCache;
                };
                options.Events.OnCustomizeSecurityCriteriaOperator = context => {
                    if (context.Operator is FunctionOperator functionOperator) {
                        if (functionOperator.Operands.Count == 1 &&
                          "CurrentCompanyOid".Equals((functionOperator.Operands[0] as ConstantValue)?.Value?.ToString(), StringComparison.InvariantCultureIgnoreCase)) {
                            context.Result = new ConstantValue(((ApplicationUser)context.Security.User)?.Company?.ID ?? Guid.NewGuid());
                        }
                    }
                };
            })
            .UsePasswordAuthentication();
        builder.AddBuildStep(application => {
            application.ConnectionString = connectionString;
#if DEBUG
            if(System.Diagnostics.Debugger.IsAttached && application.CheckCompatibilityType == CheckCompatibilityType.DatabaseSchema) {
                application.DatabaseUpdateMode = DatabaseUpdateMode.UpdateDatabaseAlways;
            }
#endif
        });
        var winApplication = builder.Build();
        return winApplication;
    }

    XafApplication IDesignTimeApplicationFactory.Create()
        => BuildApplication(XafApplication.DesignTimeConnectionString);
}
