// Copyright (c) Rotorz Limited. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root.

using System;

namespace com.ktgame.utils.class_type_reference
{
    [AttributeUsage(AttributeTargets.Field| AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ClassExtendsAttribute : ClassTypeConstraintAttribute
    {
        public ClassExtendsAttribute() { }
        
        public ClassExtendsAttribute(Type baseType)
        {
            this.BaseType = baseType;
        }

        
        public Type BaseType { get; private set; }


        /// <inheritdoc/>
        public override bool IsConstraintSatisfied(Type type)
        {
            if (!base.IsConstraintSatisfied(type))
            {
                return false;
            }
            
            if (this.BaseType == null)
            {
                return true;
            }

            return this.BaseType.IsAssignableFrom(type) && type != this.BaseType;
        }
    }
}