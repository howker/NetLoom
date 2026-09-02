using System;
using System.Collections.Generic;
using NetLoom.Domain.Locations;

namespace NetLoom.Application.Locations
{
    public interface ILocationRepository
    {
        void Save(Location location);

        Location Get(Guid id);

        IReadOnlyList<Location> GetAll();

        void Delete(Guid id);
    }
}
