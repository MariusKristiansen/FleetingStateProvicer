using System.Linq.Expressions;

namespace Ikomm.Portal.Admin.Shared.State;
public class FleetingStateProvider : IFleetingStateProvider
{

    public HashSet<IState> States { get; private set; }
    public List<IFeature> Anchors { get; private set; }
    private int _maxStateCount = 2;

    public FleetingStateProvider()
    {
        States = new();
        Anchors = new();
    }
    
    private IFeature<T> GetOrCreateAnchor<T>()
    {
        return (IFeature<T>)Anchors.FirstOrDefault(f => f is IFeature<T>) ?? new Anchor<T>();
    }

    public IState<T> GetState<T>() {
        var anchor = GetOrCreateAnchor<T>();
        if (States.Any(f => f is IState<T>))
        {
            return (IState<T>)States.FirstOrDefault(s => s is IState<T>);
        } 
        else
        {
            if (States.Count >= _maxStateCount) throw new Exception("Max count of states reached");
            IState<T> state = new State<T>(anchor);
            States.Add(state);
            Anchors.Add(anchor);
            return state;
        }
    }

    public bool RemoveState<T>()
    {
        return States.Any(f => f is IFeature<T>) && States.Remove(GetState<T>());
    }
}

public interface IFleetingStateProvider
{
    public IState<T> GetState<T>();
    public bool RemoveState<T>();
    public HashSet<IState> States { get; }
    public List<IFeature> Anchors { get; }
}

public static class ServiceExtensions
{
    public static void AddFleetingStates(this IServiceCollection services)
    {
        services.AddSingleton<IFleetingStateProvider, FleetingStateProvider>();
    }
}

public class Anchor<T> : Feature<T>
{
    public override string GetName() => nameof(Anchor<T>);
    protected override T GetInitialState() => Activator.CreateInstance<T>();
}

public class BaseAction<T> : BaseAction 
{
    public BaseAction(T newstate) : base(newstate) //? Redunadant
    {
        NewState = newstate;
    }

    public BaseAction(T oldState, Expression<Func<T, object>> update, object newValue) : base(oldState) //? Redunadant
    {
        var prop = GetPropertyFromExpression(update);
        prop.SetValue(oldState, newValue);
        NewState = oldState;
    }

    public new T NewState { get; set; }
}

public abstract class BaseAction 
{
    public object NewState { get; set; }
    public BaseAction(object newstate)
    {
        NewState = newstate;
    }
    
    public static PropertyInfo GetPropertyFromExpression<T>(Expression<Func<T, object>> property)
    {
        MemberExpression memberExpression;
        if (property.Body is UnaryExpression unaryExpression)
        {
            memberExpression = (MemberExpression)(unaryExpression.Operand);
        }
        else
        {
            memberExpression = (MemberExpression)property.Body;
        }
        PropertyInfo prop = (PropertyInfo)memberExpression.Member;
        return prop is null ? throw new Exception(property.ToString()) : prop;
    }
}

public static class StateExtensions
{
    public static BaseAction<T> UpdateValue<T>(this IState<T> state, Expression<Func<T, object>> update, object newValue)
    {
        return new BaseAction<T>(state.Value, update, newValue);
    }

    public static void UpdateValue<T>(this IDispatcher dispatcher, IState<T> state, Expression<Func<T, object>> update, object newValue)
    {
        dispatcher.Dispatch(state.UpdateValue(update, newValue));
    }

    public static void UpdateValue<T>(this IDispatcher dispatcher, T newState)
    {
        dispatcher.Dispatch(new BaseAction<T>(newState));
    }
}

public class TestEffect : Effect<BaseAction>
{
    public override Task HandleAsync(BaseAction action, IDispatcher dispatcher) 
    {
        Console.WriteLine("INTERCEPTED!");
        Console.WriteLine(action.NewState);
        return Task.CompletedTask;
    }
}

public class Reducers
{
    [ReducerMethod]
    public static T Reduce<T>(T oldState, BaseAction<T> action) =>action.NewState;    
    
}
