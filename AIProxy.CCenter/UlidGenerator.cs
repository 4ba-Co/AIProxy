using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace AIProxy.CCenter;

public sealed class UlidGenerator : ValueGenerator<string>
{
    public override string Next(EntityEntry entry)
    {
        return Ulid.NewUlid().ToString().ToLower();
    }

    public override bool GeneratesTemporaryValues => false;
}