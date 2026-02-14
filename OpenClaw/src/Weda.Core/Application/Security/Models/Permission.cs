namespace Weda.Core.Application.Security.Models;

public static class Permission
{
    // Employee permissions
    public const string EmployeeRead = "Employee.Read";
    public const string EmployeeWrite = "Employee.Write";
    public const string EmployeeDelete = "Employee.Delete";

    // User permissions
    public const string UserRead = "User.Read";
    public const string UserWrite = "User.Write";
    public const string UserDelete = "User.Delete";
    public const string UserManageRoles = "User.ManageRoles";
}
