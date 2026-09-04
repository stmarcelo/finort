using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Finort.App;

public abstract class AppComponentBase : ComponentBase
{
    protected Variant gVariant => GlobalVariant.Value;
}