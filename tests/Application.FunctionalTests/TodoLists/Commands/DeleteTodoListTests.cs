using umbral_backend.Application.TodoLists.Commands.CreateTodoList;
using umbral_backend.Application.TodoLists.Commands.DeleteTodoList;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.FunctionalTests.TodoLists.Commands;

public class DeleteTodoListTests : TestBase
{
    [Test]
    public async Task ShouldRequireValidTodoListId()
    {
        var command = new DeleteTodoListCommand(99);
        await Should.ThrowAsync<NotFoundException>(() => TestApp.SendAsync(command));
    }

    [Test]
    public async Task ShouldDeleteTodoList()
    {
        var listId = await TestApp.SendAsync(new CreateTodoListCommand
        {
            Title = "New List"
        });

        await TestApp.SendAsync(new DeleteTodoListCommand(listId));

        var list = await TestApp.FindAsync<TodoList>(listId);

        list.ShouldBeNull();
    }
}
