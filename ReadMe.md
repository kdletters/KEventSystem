# KEventSystem

这是一个轻量级的事件中心，简单易用，易扩展，性能损耗低。  

+ 事件的参数类型不做限制，推荐使用结构体。
+ 静态事件可以使用<***KEventListenerAttribute***>进行注册，在调用初始化方法后会自动注册添加特性的事件。

## 使用方法
总共只有三个方法：
+ 事件注册： <***KEventSystem.Subscribe***>
+ 事件触发： <***KEventSystem.Dispatch***>
+ 事件注销： <***KEventSystem.Unsubscribe***>
> 示例请看测试用例

------
# KEventSystem

It is a lightweight event hub that is easy to use, easy to extend, and low performance loss.

+ There is no restriction on the parameter types of events, and structs are recommended.
+ Static events can be registered using the <***KEventListenerAttribute***>, which automatically registers the event that adds the attribute after the initialization method is called.

## How to use
There are only three methods:
+ Event subscribe: <***KEventSystem.Subscribe***>
+ Event dispatch: <***KEventSystem.Dispatch***>
+ Event unsubscribe: <***KEventSystem.Unsubscribe***>
> See test cases for examples