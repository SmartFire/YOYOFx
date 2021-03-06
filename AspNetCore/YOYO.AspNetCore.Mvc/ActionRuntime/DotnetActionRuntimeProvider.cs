﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YOYO.Mvc.Reflection;
using System.Reflection;
using YOYO.Owin;
using YOYO.Mvc.Extensions;

namespace YOYO.Mvc.ActionRuntime
{
    public class DotnetActionRuntimeProvider : IActionRuntimeProvider

    {
        private static ConcurrentDictionary<MethodInfo, IDynamicMethodInvoker> actionInvokerCache = new ConcurrentDictionary<MethodInfo, IDynamicMethodInvoker>();

		public string Name{ get{ return "dotnet"; } }

		public object ExecuteAsync(string controllerName,string actionName,IOwinContext context)
		{
            var serviceProvider = Application.CurrentApplication.ServiceProvider;

            var controllerType = ApplicationAssemblyLoader.FindControllerTypeByName(controllerName);
			if (controllerType == null) throw new NullReferenceException("Not Found Controller Name by" + controllerName);

            var controllerFactory = (IControllerFacotry)serviceProvider.
                                            GetService(typeof(IControllerFacotry));
           

            var controller = controllerFactory.CreateController(controllerType, serviceProvider);
           
            controller.SetHttpContext(context);

            var actionMethodInfo = controllerType.GetTypeInfo().GetMethod(actionName);
			if (actionMethodInfo == null) throw new NullReferenceException("Not Found Action Name by" + actionName);

			IDynamicMethodInvoker invoker = null;
			if (!actionInvokerCache.TryGetValue(actionMethodInfo, out invoker))
			{
				invoker = new DynamicMethodInvoker(actionMethodInfo);
				actionInvokerCache.TryAdd(actionMethodInfo, invoker);
			}

            var parameterInfoList = actionMethodInfo.GetParameters();

            var actionParams = parameterInfoList.ToActionParameters();

            var paramValues = ActionRuntimeParameter.GetValuesByRequest(actionParams, context.Request);

            var filter = actionMethodInfo.GetCustomAttribute(typeof(ActionFilterAttribute), true) as ActionFilterAttribute;
            object result = null;
            if (filter != null)
            {
                ActionExecuteContext executeContext = new ActionExecuteContext(context, controllerName, actionName, invoker, parameterInfoList);
               
                if (filter.OnActionExecuting(executeContext))
                    executeContext.Result = invoker.Invoke(controller, paramValues.ToArray());

                filter.OnActionExecuted(executeContext);
                result = executeContext.Result;
            }
            else
            {
                result = invoker.Invoke(controller, paramValues.ToArray());
            }

			return result;
		}


      


		public string[] GetControllerNames()
		{

			return ApplicationAssemblyLoader.GetControllerNames();
		}



        public void LoadRuntime(string path)
        {
            //ApplicationAssemblyLoader.ResolveAssembly(path);
        }
    }
}
