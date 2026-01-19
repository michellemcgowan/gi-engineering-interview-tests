using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Test1.Contracts;
using Test1.Core;
using Test1.Models;
using static Test1.Controllers.MembersController;


namespace Test1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase 
    {
        private readonly ISessionFactory _sessionFactory;

        public AccountsController(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        // GET: api/accounts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountDto>>> List(CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            const string sql = @"
SELECT
    account.Guid,
    account.Status,
    account.PaymentAmount,
    account.PeriodStartUtc,
    account.PeriodEndUtc,
    account.NextBillingUtc
FROM
    account
JOIN
    location ON account.LocationUid = location.UID;
";
            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql);

            var rows = await dbContext.Session.QueryAsync<AccountDto>(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            return Ok(rows); // Returns an HTTP 200 OK status with the data
        }

        // GET: api/accounts/{Guid}
        [HttpGet("{id:Guid}")]
        public async Task<ActionResult<AccountDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            const string sql = @"
SELECT
    account.Guid,
    account.Status,
    account.PaymentAmount,
    account.PeriodStartUtc,
    account.PeriodEndUtc,
    account.NextBillingUtc
FROM
    account
JOIN
    location ON account.LocationUid = location.UID;
";

            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql);

            builder.Where("Guid = @Guid", new
            {
                Guid = id
            });

            var rows = await dbContext.Session.QueryAsync<AccountDto>(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            return Ok(rows.FirstOrDefault()); // Returns an HTTP 200 OK status with the data
        }

        // POST: api/accounts
        [HttpPost]
        public async Task<ActionResult<string>> Create([FromBody] AccountDto model, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            const string sql = @"
INSERT INTO account (
    Guid,
    CreatedUtc,
    Status,
    AccountType,
    PaymentAmount,
    PendCancel,,
    PeriodStartUtc,
    PeriodEndUtc,
    NextBillingUtc,
) VALUES (
    @Guid,
    @LocationUid,
    @Status,
    @AccountType,
    @PaymentAmount,
    @PendCancel,
    @PeriodStartUtc,
    @PeriodEndUtc,
    @NextBillingUtc,
);";

            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql, new
            {
                Guid = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Status = model.Status,
                PaymentAmount = model.PaymentAmount,
                PendCancel = model.PendCancel,
                AccountType = model.AccountType,
                PeriodStartUtc = model.PeriodStartUtc,
                PeriodEndUtc = model.PeriodEndUtc,
                NextBillingUtc = DateTime.UtcNow.AddMonths(1)
            });

            var count = await dbContext.Session.ExecuteAsync(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            if (count == 1)
                return Ok();
            else
                return BadRequest("Unable to add account");
        }


        // POST: api/accounts/id
        // update account by Guid
        [HttpPost("{id:Guid}")]
        public async Task<ActionResult<string>> UpdateAccount(Guid id, [FromBody] AccountDto model, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            const string sql = @"
SELECT
    UID,
    LocationUid,
    Guid,
    CreatedUtc,
    UpdatedUtc,
    Status,
    EndDateUtc,
    AccountType,
    PaymentAmount,
    PendCancel,
    PeriodStartUtc,
    PeriodEndUtc,
    NextBillingUtc
FROM account";

            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql);

            builder.Where("Guid = @Guid", new
            {
                Guid = id
            });

            var rows = await dbContext.Session.QueryAsync<AccountDto>(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            var existingAccount = rows.FirstOrDefault();

            if (existingAccount == null)
            {
                return NotFound("Account not found");
            }

            var updateBuilder = new SqlBuilder();
            const string updateSql = @"
UPDATE account SET
    Status = @Status,
    EndDateUtc = @EndDateUtc,
    AccountType = @AccountType,
    PaymentAmount = @PaymentAmount,
    PendCancel = @PendCancel,
    PendCancelDateUtc = @PendCancelDateUtc,
    PeriodStartUtc = @PeriodStartUtc,
    NextBillingUtc = @NextBillingUtc
WHERE
    Guid = @Guid       
            ";

            var updateTemplate = updateBuilder.AddTemplate(updateSql, new
            {
                Guid = id,
                Status = model.Status,
                PaymentAmount = model.PaymentAmount,
                PendCancel = model.PendCancel,
                AccountType = model.AccountType,
                PeriodStartUtc = model.PeriodStartUtc,
                PeriodEndUtc = model.PeriodEndUtc,
                NextBillingUtc = model.NextBillingUtc
            });

            var count = await dbContext.Session.ExecuteAsync(updateTemplate.RawSql, updateTemplate.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            if (count == 1)
                return Ok();
            else
                return BadRequest("Unable to update account");
        }

        // DELETE: api/accounts/{Guid}
        [HttpDelete("{id:Guid}")]
        public async Task<ActionResult<AccountDto>> DeleteById(Guid id, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);
            // TODO: ask about this!!!! b/c of foreign key constraints
            const string sql = @"
DELETE FROM account WHERE Guid = @Guid;
AND account.UID 
IN (SELECT UID FROM location WHERE location.UID = @LocationUid);
";

            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql, new
            {
                Guid = id
            });

            var count = await dbContext.Session.ExecuteAsync(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            if (count == 1)
                return Ok();
            else
                return BadRequest("Unable to delete account");
        }

        // GET /api/accounts/{id}/members
        // get all members associated with an account
        [HttpGet("{id:Guid}/members")]
        public async Task<ActionResult<IEnumerable<MemberDto>>> GetAllMembers(Guid id, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            const string sql = @"
SELECT
    member.Guid,
    member.FirstName,
    member.LastName,
    member.Address,
    member.City,
    member.Locale,
    member.PostalCode,
    member.JoinedDateUtc,
    member.CancelDateUtc,
    member.Cancelled
FROM member
JOIN account ON member.AccountUid = account.UID
WHERE account.Guid = @AccountGuid;";

            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql, new
            {
                AccountGuid = id
            });

            var rows = await dbContext.Session.QueryAsync<MemberDto>(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            return Ok(rows);
        }

        // DELETE /api/accounts/{id}/members
        // delete all members associated with an account except primary member
        [HttpDelete("{id:Guid}/members")]
        public async Task<ActionResult<IEnumerable<MemberDto>>> DeleteAllMembers(Guid id, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            const string sql = @"
DELETE
FROM member
WHERE member.""Primary"" = 0
AND member.AccountUid 
IN (SELECT UID FROM account WHERE account.Guid = @AccountGuid);";

            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql, new
            {
                AccountGuid = id
            });

            var count = await dbContext.Session.ExecuteAsync(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            if (count == 1)
                return Ok();
            else
                return BadRequest("Unable to delete members from account");
        }

        public class AccountDto 
        {
            public Guid Guid {get;set;}
            public string Status {get;set;}
            public string PaymentAmount {get;set;}
            public string PeriodStartUtc {get;set;}
            public string PeriodEndUtc {get;set;}
            public string NextBillingUtc {get;set;}
            public string PendCancel {get;set;}
            public string AccountType {get;set;}
        }

        
    }
}
