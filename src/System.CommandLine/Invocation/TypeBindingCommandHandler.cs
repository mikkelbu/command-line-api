﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Threading.Tasks;

namespace System.CommandLine.Invocation
{
    internal class TypeBindingCommandHandler : ICommandHandler
    {
        private readonly MethodInfo _onExecuteMethodInfo;
        private readonly TypeBinder _typeBinder;

        public TypeBindingCommandHandler(
            MethodInfo method,
            TypeBinder typeBinder)
        {
            _onExecuteMethodInfo = method ?? throw new ArgumentNullException(nameof(method));
            _typeBinder = typeBinder;
        }

        public Task<int> InvokeAsync(InvocationContext context)
        {
            var instance = _typeBinder.CreateInstance(context);

            var methodBinder = new MethodBinder(_onExecuteMethodInfo, instance);

            return methodBinder.InvokeAsync(context);

        }
    }
}
