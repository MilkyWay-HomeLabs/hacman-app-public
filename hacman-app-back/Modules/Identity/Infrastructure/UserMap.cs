using hacman_app_back.Modules.Identity.Domain;
using NHibernate;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;

namespace hacman_app_back.Modules.Identity.Infrastructure;

public class UserMap : ClassMapping<User>
{
    public UserMap()
    {
        Table("users");
        Id(x => x.Id, m =>
        {
            m.Generator(Generators.GuidComb);
            m.Column("id");
            m.Type(NHibernateUtil.Guid);
        });
        Property(x => x.Username, m =>
        {
            m.Column("username");
            m.Length(255);
            m.NotNullable(true);
        });
        Property(x => x.Email, m =>
        {
            m.Column("email");
            m.Length(255);
            m.NotNullable(true);
        });
    }
}