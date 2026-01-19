using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Test1.Contracts;
using Test1.Core;

namespace Test1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembersController : ControllerBase
    {
        private readonly ISessionFactory _sessionFactory;

        public MembersController(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        // GET: api/members
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MemberDto>>> List(CancellationToken cancellationToken)
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
    member.""Primary"",
    member.Locale,
    member.PostalCode,
    member.JoinedDateUtc,
    member.CancelDateUtc,
    member.Cancelled
FROM
    member
LEFT JOIN
    account ON member.AccountUid = account.UID;
";

            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql);

            var rows = await dbContext.Session.QueryAsync<MemberDto>(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            return Ok(rows); // Returns an HTTP 200 OK status with the data
        }


        // POST: api/members
        [HttpPost]
        public async Task<ActionResult<string>> Create([FromBody] MemberDto model, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            const string sql = @"
INSERT INTO member (
    Guid,
    CreatedUtc,
    Disabled,
    EnableBilling,
    AccountStatus,
    FirstName,
    LastName,
    Address,
    City,
    Locale,
    PostalCode,
    Primary
) VALUES (
    @Guid,
    @CreatedUtc,
    @Disabled,
    @EnableBilling,
    @AccountStatus,
    @FirstName,
    @LastName,
    @Address,
    @City,
    @Locale,
    @PostalCode
    @Primary
)
ADD CONSTRAINT ACCOUNT_OnePrimary UNIQUE (AccountUid, Primary)
WHERE Primary = 1
;";

            var builder = new SqlBuilder();

            var template = builder.AddTemplate(sql, new
            {
                Guid = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Disabled = false,
                EnableBilling = false,
                model.FirstName,
                model.LastName,
                model.Address,
                model.City,
                model.Locale,
                model.PostalCode, 
            });

            var count = await dbContext.Session.ExecuteAsync(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            if (count == 1)
                return Ok();
            else
                return BadRequest("Unable to add member");
        }

        // DELETE: api/members/{Guid}
        [HttpDelete("{id:Guid}")]
        public async Task<ActionResult<MemberDto>> DeleteById(Guid id, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);
    // TODO: ask about this 
            const string sql = @"
DELETE FROM member WHERE Guid = @Guid;
DELETE FROM account WHERE LocationUid = @Guid;
DELETE FROM location WHERE UID = @Guid;";

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
                return BadRequest("Unable to delete member");
        }
        public class MemberDto
        {
            public Guid Guid { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string Locale { get; set; }
            public string PostalCode { get; set; }
            public bool Primary { get; set; }
            public DateTime JoinedDateUtc { get; set; }
            public DateTime? CancelDateUtc { get; set; }
            public bool Cancelled { get; set; }
        }
    }

}
