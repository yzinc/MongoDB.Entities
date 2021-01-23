using System.Threading.Tasks;

namespace MongoDB.Entities.Tests
{
    [Migration(3, "Test Attribute")]
    public class NewMigration : IMigration
    {
        public async Task UpgradeAsync()
        {
            await DB.Update<Book>()
                .Match(_ => true)
                .Modify(b => b.Rename("SellingPrice", "SellingPriceNew"))
                .ExecuteAsync().ConfigureAwait(false);
        }
    }
}
