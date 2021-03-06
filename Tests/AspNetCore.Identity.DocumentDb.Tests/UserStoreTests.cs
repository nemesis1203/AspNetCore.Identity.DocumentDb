﻿using AspNetCore.Identity.DocumentDb;
using AspNetCore.Identity.DocumentDb.Stores;
using AspNetCore.Identity.DocumentDb.Tests.Builder;
using AspNetCore.Identity.DocumentDb.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCore.Identity.DocumentDb.Tests
{
    [Collection("DocumentDbCollection")]
    public class UserStoreTests : StoreTestsBase
    {
        public UserStoreTests(DocumentDbFixture documentDbFixture) 
            : base(documentDbFixture)
        {
            this.collectionUri = UriFactory.CreateDocumentCollectionUri(this.documentDbFixture.Database, this.documentDbFixture.UserStoreDocumentCollection);
        }

        [Fact]
        public async Task ShouldSetNormalizedUserName()
        {
            DocumentDbIdentityUser<DocumentDbIdentityRole> user = DocumentDbIdentityUserBuilder.Create().WithNormalizedUserName();
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();

            string normalizedUserName = Guid.NewGuid().ToString();
            await store.SetNormalizedUserNameAsync(user, normalizedUserName, CancellationToken.None);

            Assert.Equal(normalizedUserName, user.NormalizedUserName);
        }

        [Fact]
        public async Task ShouldReturnAllUsersWithAdminRoleClaim()
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();

            string adminRoleValue = Guid.NewGuid().ToString();

            DocumentDbIdentityUser<DocumentDbIdentityRole> firstAdmin = DocumentDbIdentityUserBuilder.Create().WithId().AddClaim(ClaimTypes.Role, adminRoleValue).AddClaim();
            DocumentDbIdentityUser<DocumentDbIdentityRole> secondAdmin = DocumentDbIdentityUserBuilder.Create().WithId().AddClaim(ClaimTypes.Role, adminRoleValue).AddClaim().AddClaim();
            DocumentDbIdentityUser<DocumentDbIdentityRole> thirdAdmin = DocumentDbIdentityUserBuilder.Create().WithId().AddClaim(ClaimTypes.Role, adminRoleValue);

            CreateDocument(firstAdmin);
            CreateDocument(secondAdmin);
            CreateDocument(DocumentDbIdentityUserBuilder.Create().AddClaim().AddClaim());
            CreateDocument(DocumentDbIdentityUserBuilder.Create().AddClaim().AddClaim().AddClaim());
            CreateDocument(DocumentDbIdentityUserBuilder.Create().AddClaim().AddClaim());
            CreateDocument(thirdAdmin);
            CreateDocument(DocumentDbIdentityUserBuilder.Create().AddClaim().AddClaim().AddClaim().AddClaim());

            IList<DocumentDbIdentityUser<DocumentDbIdentityRole>> adminUsers = await store.GetUsersForClaimAsync(new Claim(ClaimTypes.Role, adminRoleValue), CancellationToken.None);
            
            Assert.Collection(
                adminUsers,
                u => u.Id.Equals(firstAdmin.Id),
                u => u.Id.Equals(secondAdmin.Id),
                u => u.Id.Equals(thirdAdmin.Id));
        }

        [Fact]
        public async Task ShouldReturnUserByLoginProvider()
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> targetUser = DocumentDbIdentityUserBuilder.Create().WithId().WithUserLoginInfo(amount: 3);
            UserLoginInfo targetLogin = targetUser.Logins[1];

            CreateDocument(DocumentDbIdentityUserBuilder.Create().WithId().WithUserLoginInfo(amount: 2));
            CreateDocument(targetUser);
            CreateDocument(DocumentDbIdentityUserBuilder.Create().WithId().WithUserLoginInfo(amount: 2));

            DocumentDbIdentityUser<DocumentDbIdentityRole> foundUser = await store.FindByLoginAsync(targetLogin.LoginProvider, targetLogin.ProviderKey, CancellationToken.None);

            Assert.Equal(targetUser.Id, foundUser.Id);
        }

        [Fact]
        public async Task ShouldReturnUserIsInRole()
        {
            DocumentDbIdentityRole role = DocumentDbIdentityRoleBuilder.Create();

            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> user = DocumentDbIdentityUserBuilder.Create().WithId().AddRole(role).AddRole().AddRole();

            bool result = await store.IsInRoleAsync(user, role.Name, CancellationToken.None);

            Assert.True(result);
        }

        [Fact]
        public async Task ShouldReturnUserIsNotInRole()
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> user = DocumentDbIdentityUserBuilder.Create().WithId().AddRole().AddRole();

            bool result = await store.IsInRoleAsync(user, Guid.NewGuid().ToString(), CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task ShouldReturnAllUsersWithAdminRole()
        {
            DocumentDbIdentityRole role = DocumentDbIdentityRoleBuilder.Create();

            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();

            DocumentDbIdentityUser<DocumentDbIdentityRole> firstAdmin = DocumentDbIdentityUserBuilder.Create().WithId().AddRole(role).AddRole();
            DocumentDbIdentityUser<DocumentDbIdentityRole> secondAdmin = DocumentDbIdentityUserBuilder.Create().WithId().AddRole(role).AddRole().AddRole();
            DocumentDbIdentityUser<DocumentDbIdentityRole> thirdAdmin = DocumentDbIdentityUserBuilder.Create().WithId().AddRole(role);

            CreateDocument(firstAdmin);
            CreateDocument(secondAdmin);
            CreateDocument(DocumentDbIdentityUserBuilder.Create().AddRole().AddRole());
            CreateDocument(DocumentDbIdentityUserBuilder.Create().AddRole().AddRole().AddRole());
            CreateDocument(thirdAdmin);
            CreateDocument(DocumentDbIdentityUserBuilder.Create());
            CreateDocument(DocumentDbIdentityUserBuilder.Create().AddRole());

            IList<DocumentDbIdentityUser<DocumentDbIdentityRole>> adminUsers = await store.GetUsersInRoleAsync(role.Name, CancellationToken.None);

            Assert.Collection(
                adminUsers,
                u => u.Id.Equals(firstAdmin.Id),
                u => u.Id.Equals(secondAdmin.Id),
                u => u.Id.Equals(thirdAdmin.Id));
        }

        [Fact]
        public async Task ShouldReturnUserBySpecificEmail()
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> targetUser = DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedEmail();

            CreateDocument(DocumentDbIdentityUserBuilder.Create());
            CreateDocument(targetUser);
            CreateDocument(DocumentDbIdentityUserBuilder.Create());
            CreateDocument(DocumentDbIdentityUserBuilder.Create());

            DocumentDbIdentityUser<DocumentDbIdentityRole> foundUser = await store.FindByEmailAsync(targetUser.NormalizedEmail, CancellationToken.None);

            Assert.Equal(targetUser.Id, foundUser.Id);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public async Task ShouldIncreaseAccessFailedCountBy1(int accessFailedCount)
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> targetUser = DocumentDbIdentityUserBuilder.Create().WithAccessFailedCountOf(accessFailedCount);

            await store.IncrementAccessFailedCountAsync(targetUser, CancellationToken.None);

            Assert.Equal(++accessFailedCount, targetUser.AccessFailedCount);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(1)]
        [InlineData(0)]
        public async Task ShouldResetAccessFailedCountToZero(int accessFailedCount)
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> store = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> targetUser = DocumentDbIdentityUserBuilder.Create().WithAccessFailedCountOf(accessFailedCount);

            await store.ResetAccessFailedCountAsync(targetUser, CancellationToken.None);

            Assert.Equal(0, targetUser.AccessFailedCount);
        }

        [Fact]
        public async Task ShouldAddUserToRole()
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> userStore = CreateUserStore();
            DocumentDbRoleStore<DocumentDbIdentityRole> roleStore = CreateRoleStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> targetUser = DocumentDbIdentityUserBuilder.Create();
            DocumentDbIdentityRole targetRole = DocumentDbIdentityRoleBuilder.Create().WithId().WithNormalizedRoleName();

            // Create sample data role
            await roleStore.CreateAsync(targetRole, CancellationToken.None);

            // Add the created sample data role to the user
            await userStore.AddToRoleAsync(targetUser, targetRole.Name, CancellationToken.None);

            Assert.Contains(targetUser.Roles, r => r.Name.Equals(targetRole.Name));
        }

        [Fact]
        public async Task ShouldThrowExceptionOnAddingUserToNonexistantRole()
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> userStore = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> targetUser = DocumentDbIdentityUserBuilder.Create();
            DocumentDbIdentityRole targetRole = DocumentDbIdentityRoleBuilder.Create().WithId().WithNormalizedRoleName();
            string roleName = Guid.NewGuid().ToString();

            // Add the created sample data role to the user
            await Assert.ThrowsAsync(typeof(ArgumentException), async () => await userStore.AddToRoleAsync(targetUser, targetRole.Name, CancellationToken.None));
        }

        [Fact]
        public async Task ShouldReturnUserByUserName()
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> userStore = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> targetUser = DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedUserName();

            CreateDocument(DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedUserName());
            CreateDocument(DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedUserName());
            CreateDocument(targetUser);
            CreateDocument(DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedUserName());

            DocumentDbIdentityUser<DocumentDbIdentityRole> foundUser = await userStore.FindByNameAsync(targetUser.NormalizedUserName, CancellationToken.None);

            Assert.Equal(targetUser.Id, foundUser.Id);
        }

        [Fact]
        public async Task ShouldReturnUserByEmail()
        {
            DocumentDbUserStore<DocumentDbIdentityUser<DocumentDbIdentityRole>, DocumentDbIdentityRole> userStore = CreateUserStore();
            DocumentDbIdentityUser<DocumentDbIdentityRole> targetUser = DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedEmail();

            CreateDocument(DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedEmail());
            CreateDocument(DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedEmail());
            CreateDocument(targetUser);
            CreateDocument(DocumentDbIdentityUserBuilder.Create().WithId().WithNormalizedEmail());

            DocumentDbIdentityUser<DocumentDbIdentityRole> foundUser = await userStore.FindByEmailAsync(targetUser.NormalizedEmail, CancellationToken.None);

            Assert.Equal(targetUser.Id, foundUser.Id);
        }
    }
}
