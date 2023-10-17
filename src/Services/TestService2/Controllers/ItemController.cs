using EventBus.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TestService2.IntegrationEvents.Events;

namespace TestService2.Controllers;
[Route("api/[controller]")]
[ApiController]
public class ItemController : ControllerBase
{
    private readonly IEventBus _eventBus;

    public ItemController(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public class UpdateItem
    {
        public int Id { get; set; }
        public int NewValue { get; set; }
        public int OldValue { get; set; }
    }

    [HttpPost]
    public ActionResult UpdateValue([FromBody] UpdateItem item)
    {
        var evt = new ItemChangeValueEvent(item.Id, item.NewValue, item.OldValue);

        _eventBus.Publish(evt);

        return Ok();
    }
}
