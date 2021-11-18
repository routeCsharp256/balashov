﻿using System;
using System.Collections.Generic;
using System.Linq;
using OzonEdu.MerchandiseService.Domain.AggregationModels.EmployeeAggregate;
using OzonEdu.MerchandiseService.Domain.AggregationModels.ManagerAggregate;
using OzonEdu.MerchandiseService.Domain.AggregationModels.MerchAggregate;
using OzonEdu.MerchandiseService.Domain.AggregationModels.ValueObjects;

namespace OzonEdu.MerchandiseService.Domain.DomainServices
{
    public sealed class MerchandiseRequestFactory
    {
        public static MerchandiseRequest CreateMerchandiseRequest(List<Manager> managers, Employee employee, MerchPack merchPack)
        {
            if (!managers.Any(m => m.CanHandleNewTask))
            {
                throw new Exception("No vacant managers");
            }

            var responsibleManager = managers.OrderBy(m => m.AssignedTasks).First();

            return CreateMerchandiseRequest(responsibleManager, employee, merchPack);
        }

        public static MerchandiseRequest CreateMerchandiseRequest(Manager manager, Employee employee, MerchPack merchPack)
        {
            var date = new Date(DateTime.Now);

            var merchRequest = new MerchandiseRequest(manager.Id, manager.PhoneNumber, merchPack, date);

            merchRequest.AddEmployeeInfo(employee.Id, employee.PhoneNumber, employee.Size, date);

            manager.AssignTask();

            return merchRequest;
        }
    }
}
