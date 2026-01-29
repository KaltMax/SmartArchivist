using AutoMapper;
using Microsoft.Extensions.Logging;
using SmartArchivist.Application.Mapping;

namespace Tests.SmartArchivist.ApplicationTests
{
    public class AutoMapperTest
    {
        [Fact]
        void TestMapper_ShouldBeValid()
        {
            var loggerFactory = LoggerFactory.Create(_ => { });

            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperConfig>();
            }, loggerFactory);
                
            config.AssertConfigurationIsValid();
        }
    }
}
