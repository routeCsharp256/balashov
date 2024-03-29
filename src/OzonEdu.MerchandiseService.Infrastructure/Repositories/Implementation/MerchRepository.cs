﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using OzonEdu.MerchandiseService.Domain.AggregationModels.Enumerations;
using OzonEdu.MerchandiseService.Domain.AggregationModels.MerchAggregate;
using OzonEdu.MerchandiseService.Domain.AggregationModels.ValueObjects;
using OzonEdu.MerchandiseService.Infrastructure.Repositories.Infrastructure.Interfaces;

namespace OzonEdu.MerchandiseService.Infrastructure.Repositories.Implementation
{
    public class MerchRepository : IMerchRepository
    {
        private readonly IDbConnectionFactory<NpgsqlConnection> _dbConnectionFactory;
        private readonly IChangeTracker _changeTracker;
        private const int Timeout = 5;
        public MerchRepository(IDbConnectionFactory<NpgsqlConnection> dbConnectionFactory, IChangeTracker changeTracker)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _changeTracker = changeTracker;
        }

        public async Task<MerchandiseRequest> CreateAsync(MerchandiseRequest itemToCreate, CancellationToken cancellationToken = default)
        {
            string sql = @"
                INSERT INTO merchandise_requests (merch_request_status_id, hr_manager_id, employee_email, pack_title_id, clothing_size_id, last_change_date)
                VALUES (@status, @hrid, @email, @pack, @size, @lastdate) RETURNING id;";

            var parameters = new
            {
                status = itemToCreate.Status.Status.Id,
                hrid = itemToCreate.HRManagerId,
                email = itemToCreate.EmployeeEmail.EmailString,
                pack = itemToCreate.RequestedMerchPack.PackTitle.Id,
                size = itemToCreate.Size.Id,
                lastdate = itemToCreate.Status.Date.ToDateTime()
            };

            var commandDefinition = new CommandDefinition(
                commandText: sql,
                parameters: parameters,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);

            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);
            var obj = await connection.QueryFirstOrDefaultAsync<long>(commandDefinition);

            if (obj != default)
                itemToCreate.SetId(obj);
            else
            {
                throw new Exception("Запись в базу заявки на мерч провалилась, Id не бы возваращён");
            }

            _changeTracker.Track(itemToCreate);
            return itemToCreate;
        }

        public async Task<MerchandiseRequest> UpdateAsync(MerchandiseRequest itemToUpdate, CancellationToken cancellationToken = default)
        {
            const string sql = @"
            UPDATE merchandise_requests
            SET merch_request_status_id = @status, hr_manager_id = @hrid, employee_email = @email,
            pack_title_id = @pack, clothing_size_id = @size, last_change_date = @lastdate
            WHERE merchandise_requests.id = @mrid;";

            var parameters = new
            {
                mrid = itemToUpdate.Id,
                status = itemToUpdate.Status.Status.Id,
                hrid = itemToUpdate.HRManagerId,
                email = itemToUpdate.EmployeeEmail.EmailString,
                pack = itemToUpdate.RequestedMerchPack.PackTitle.Id,
                size = itemToUpdate.Size.Id,
                lastdate = itemToUpdate.Status.Date.ToDateTime()
            };
            var commandDefinition = new CommandDefinition(
                commandText: sql,
                parameters: parameters,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);

            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);

            await connection.ExecuteAsync(commandDefinition);

            _changeTracker.Track(itemToUpdate);

            return itemToUpdate;
        }

        public async Task<MerchandiseRequest> FindByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            const string sql = @"SELECT merchandise_requests.id, merchandise_requests.hr_manager_id, merchandise_requests.employee_email,
            merchandise_requests.clothing_size_id, merchandise_requests.pack_title_id, merchandise_requests.merch_request_status_id, merchandise_requests.last_change_date,
            merchandise_requests.employee_email
            FROM merchandise_requests
            WHERE merchandise_requests.id = @mrId;";

            var parameters = new { mrId = id };

            var merchRequest = (await DoRequest_GetMerchandiseRequest(sql, cancellationToken, parameters)).First();
                _changeTracker.Track(merchRequest);

            return merchRequest;
        }

        public async Task<List<MerchandiseRequest>> FindByEmployeeEmailAsync(string employeeEmail, CancellationToken cancellationToken = default)
        {
            const string sql = @"SELECT merchandise_requests.id, merchandise_requests.hr_manager_id,merchandise_requests.employee_email,
            merchandise_requests.clothing_size_id, merchandise_requests.pack_title_id, merchandise_requests.merch_request_status_id, merchandise_requests.last_change_date
            FROM merchandise_requests
            WHERE merchandise_requests.employee_email = @email;";

            var parameters = new { email = employeeEmail };

            var merchRequests = (await DoRequest_GetMerchandiseRequest(sql, cancellationToken, parameters)).ToList();

            return merchRequests.ToList();
        }

        private async Task<IEnumerable<MerchandiseRequest>> DoRequest_GetMerchandiseRequest(
            string sql,
            CancellationToken cancellationToken,
            object parameters = null)
        {
            var commandDefinition = new CommandDefinition(
                commandText: sql,
                parameters: parameters,
                commandTimeout: Timeout,
                cancellationToken: cancellationToken);

            var connection = await _dbConnectionFactory.CreateConnection(cancellationToken);

            var dbElements = await connection.QueryAsync<Models.MerchandiseRequest>(commandDefinition);

            var merchRequests = dbElements.Select(dbMerchandiseRequest => new MerchandiseRequest(
                    dbMerchandiseRequest.HrManagerId,
                    dbMerchandiseRequest.PackTitleId is not null
                        ? new MerchPack((int)dbMerchandiseRequest.PackTitleId)
                        : null,
                    MerchRequestStatusType.GetById(dbMerchandiseRequest.MerchRequestStatusId),
                    new Date(dbMerchandiseRequest.LastChangeDate))
                .AddEmployeeInfoFromDB(
                    new Email(dbMerchandiseRequest.EmployeeEmail),
                    dbMerchandiseRequest.ClothingSizeId is not null
                        ? Size.GetById((int)dbMerchandiseRequest.ClothingSizeId)
                        : null)
                .SetId(dbMerchandiseRequest.Id));

            return merchRequests;
        }
    }
}
