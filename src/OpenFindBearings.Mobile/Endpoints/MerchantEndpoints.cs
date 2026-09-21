using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 商家相关端点
/// 代理 API 的商家搜索、详情、在售商品、入驻申请与状态查询等接口
/// </summary>
public static class MerchantEndpoints
{
    public static void MapMerchantEndpoints(this RouteGroupBuilder group)
    {

        /// <summary>
        /// 搜索商家
        /// </summary>
        group.MapGet("/search", async (
            ApiClient api,
            [AsParameters] SearchParams query,
            CancellationToken ct) =>
        {
            var path = BuildQueryString("/api/merchants/search", query);
            var result = await api.GetPagedAsync<MerchantItem>(path, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<MerchantItem>([], 0, 1, 20));
        })
        .WithName("SearchMerchants")
        .WithSummary("搜索商家")
        .AllowAnonymous();

        /// <summary>
        /// 商家详情
        /// </summary>
        group.MapGet("/{id:guid}", async (
            Guid id,
            ApiClient api,
            CancellationToken ct) =>
        {
            var result = await api.GetAsync<MerchantDetail>($"/api/merchants/{id}", ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetMerchantDetail")
        .WithSummary("获取商家详情")
        .AllowAnonymous();

        /// <summary>
        /// 商家在售商品
        /// </summary>
        group.MapGet("/{id:guid}/bearings", async (
            Guid id,
            ApiClient api,
            [AsParameters] BearingQuery query,
            CancellationToken ct) =>
        {
            var path = $"/api/merchants/{id}/bearings?page={query.Page}&pageSize={query.PageSize}";
            var result = await api.GetPagedAsync<MerchantBearingItem>(path, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<MerchantBearingItem>([], 0, 1, 20));
        })
        .WithName("GetMerchantBearings")
        .WithSummary("获取商家在售商品")
        .AllowAnonymous();

        /// <summary>
        /// 入驻申请（需登录）
        /// </summary>
        group.MapPost("/apply", async (
            ApplyRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            // 改动说明：透传用户 access token（原实现 PostAsync 不带 token 且取 sub 判断登录，API 侧拿不到身份会 401）
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            // 改动说明：改用带错误透传的 PostWithResultAsync。原 PostAsync<object> 会把 API 的
            //   所有 4xx/409 吞成 null 且端点固定返回 200，导致"已被他人认领/已认证/新建撞名"等错误
            //   前端完全看不到（虚假成功）。现按状态码与错误码原样转发。
            var result = await api.PostWithResultAsync<ApplyResultData>("/api/merchant/apply", body, token, ct);
            if (result.Success)
                return Results.Ok(new { message = result.Data?.Message ?? "申请已提交，等待审核", merchantId = result.Data?.MerchantId });

            // 可认领冲突：409 + 结构化字段，供前端弹窗引导"改为认领"
            if (result.StatusCode == 409 && result.ErrorCode == "MERCHANT_CLAIMABLE_EXISTS")
                return Results.Json(new
                {
                    success = false,
                    code = "MERCHANT_CLAIMABLE_EXISTS",
                    message = result.ErrorText ?? "库中已存在可认领的同名商户",
                    existingMerchantId = result.GetExtension("existingMerchantId"),
                    existingName = result.GetExtension("existingName")
                }, statusCode: 409);

            // 其他错误：沿用上游状态码与文案透传（无状态码时兜底 502）
            return Results.Json(new
            {
                success = false,
                message = result.ErrorText ?? "申请提交失败，请稍后重试"
            }, statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("ApplyMerchant")
        .WithSummary("商家入驻申请")
        .WithDescription("提交商家入驻申请（self 新建或 claim 认领爬虫商家），需登录")
        .RequireAuthorization();

        /// <summary>
        /// 查询入驻状态（需登录）
        /// </summary>
        group.MapGet("/application", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.GetAsync<List<MerchantApplicationItem>>("/api/merchant/application", token, ct);
            return Results.Ok(result ?? []);
        })
        .WithName("GetMerchantApplication")
        .WithSummary("查询入驻状态")
        .WithDescription("查询当前用户在各商户的入驻进度，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 申请人自助撤回待审核的入驻申请（需登录）
        /// </summary>
        group.MapPost("/{merchantId:guid}/withdraw", async (
            Guid merchantId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            // 改动说明（v1.6.1）：PostVoidAsync 吞掉上游 400 原因（"仅 Pending 可撤回/渠道不支持"等
            //   全被压成干巴巴的"撤回失败"）——改用 PostWithResultAsync 按状态码与 detail 文案透传
            var result = await api.PostWithResultAsync<object>($"/api/merchant/{merchantId}/withdraw", null, token, ct);
            if (result.Success)
                return Results.Ok(new { success = true, message = "入驻申请已撤回" });
            return Results.Json(new { success = false, message = result.ErrorText ?? "撤回失败" },
                statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("WithdrawMerchantApplication")
        .WithSummary("撤回入驻申请")
        .WithDescription("申请人撤回自己待审核的入驻申请，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 商户申请认证（v1.6.3 新增代理，管理员；透传 API POST /api/merchant/{id}/verify-request，
        /// 材料不齐时按 400 detail 透传缺项引导文案）
        /// </summary>
        group.MapPost("/{merchantId:guid}/verify-request", async (
            Guid merchantId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            var result = await api.PostWithResultAsync<object>($"/api/merchant/{merchantId}/verify-request", null, token, ct);
            if (result.Success)
                return Results.Ok(new { success = true, message = "认证申请已提交，平台将优先审核" });
            return Results.Json(new { success = false, message = result.ErrorText ?? "申请失败" },
                statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("RequestMerchantVerify")
        .WithSummary("申请商家认证")
        .WithDescription("商户管理员主动申请认证（需必备材料全部审核通过），需登录")
        .RequireAuthorization();

        /// <summary>
        /// 查询单个入驻申请详情（被拒重提预填，需登录，v1.5.0 新增）
        /// </summary>
        group.MapGet("/{merchantId:guid}/application", async (
            Guid merchantId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.GetAsync<ApplicationDetailItem>($"/api/merchant/{merchantId}/application", token, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetMerchantApplicationDetail")
        .WithSummary("查询入驻申请详情")
        .WithDescription("申请人查看自己某张入驻申请的全量资料（重提预填），需登录")
        .RequireAuthorization();

        /// <summary>
        /// 被拒后修改资料重新提交（需登录，v1.5.0 新增）
        /// </summary>
        group.MapPost("/{merchantId:guid}/resubmit", async (
            Guid merchantId,
            ResubmitRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            // 重提失败（状态已变/名称撞他人/必填缺失）沿用上游状态码与文案透传，前端 Toast 展示
            var result = await api.PostWithResultAsync<ApplyResultData>($"/api/merchant/{merchantId}/resubmit", body, token, ct);
            if (result.Success)
                return Results.Ok(new { message = result.Data?.Message ?? "修改后的申请已重新提交，等待审核" });

            return Results.Json(new
            {
                success = false,
                message = result.ErrorText ?? "重新提交失败，请稍后重试"
            }, statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("ResubmitMerchantApplication")
        .WithSummary("重新提交被拒的入驻申请")
        .WithDescription("申请人修改资料后重新提交被驳回的入驻申请，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 删除被驳回的入驻申请（需登录，v1.5.0 新增）
        /// </summary>
        group.MapPost("/{merchantId:guid}/delete-application", async (
            Guid merchantId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            // 改动说明（v1.6.1）：同 withdraw，透传"仅被驳回可删除/无权删除/渠道不支持"等真实原因
            var result = await api.PostWithResultAsync<object>($"/api/merchant/{merchantId}/delete-application", null, token, ct);
            if (result.Success)
                return Results.Ok(new { success = true, message = "被驳回的申请已删除" });
            return Results.Json(new { success = false, message = result.ErrorText ?? "删除失败" },
                statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("DeleteMerchantApplication")
        .WithSummary("删除被驳回的入驻申请")
        .WithDescription("申请人删除自己被驳回的入驻申请（新建硬删/认领退回公共池），需登录")
        .RequireAuthorization();

        /// <summary>
        /// 认领搜索爬虫商家（需登录）
        /// </summary>
        group.MapGet("/claimable", async (
            string? keyword,
            int page,
            int pageSize,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var path = $"/api/merchant/claimable?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(keyword))
                path += $"&keyword={Uri.EscapeDataString(keyword)}";

            var result = await api.GetAsync<ApiClient.PagedResult<ClaimableMerchantItem>>(path, token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<ClaimableMerchantItem>([], 0, 1, 20));
        })
        .WithName("GetClaimableMerchants")
        .WithSummary("认领搜索爬虫商家")
        .WithDescription("搜索可认领的爬虫来源商家，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 提名他人为管理员（入驻模式 B，需登录）
        /// </summary>
        group.MapPost("/nominate", async (
            NominateRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.PostAsync<object>("/api/merchant/nominate", body, token, ct);
            return Results.Ok(new { message = "提名邀请已创建，等待对方接受" });
        })
        .WithName("NominateMerchant")
        .WithSummary("提名他人为管理员")
        .WithDescription("提名另一手机号/邮箱作为商户管理员，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 接受管理员提名（被提名人补资料并提交审核，需登录）
        /// </summary>
        group.MapPost("/nominate/{code}/accept", async (
            string code,
            AcceptNominationRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.PostAsync<object>($"/api/merchant/nominate/{code}/accept", body, token, ct);
            return Results.Ok(new { message = "已接受提名，申请已提交，等待审核" });
        })
        .WithName("AcceptNomination")
        .WithSummary("接受管理员提名")
        .WithDescription("被提名人接受提名并补全商户资料，提交后台审核，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 待我接受的管理员提名列表（需登录；API 按 JWT 手机号匹配）
        /// </summary>
        group.MapGet("/nominations/pending", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.GetAsync<List<PendingNominationItem>>("/api/merchant/nominations/pending", token, ct);
            return Results.Ok(result ?? []);
        })
        .WithName("GetPendingNominations")
        .WithSummary("待我接受的提名")
        .WithDescription("查询发给当前登录用户手机号的待接受管理员提名，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 获取当前商户成员列表（需登录且为商户成员）
        /// </summary>
        group.MapGet("/staff", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.GetAsync<ApiClient.PagedResult<MerchantStaffItem>>("/api/merchant/staff?page=1&pageSize=100", token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<MerchantStaffItem>([], 0, 1, 100));
        })
        .WithName("GetMerchantStaff")
        .WithSummary("获取商户成员列表")
        .WithDescription("获取当前商户成员列表（含角色与状态），需登录且为商户成员")
        .RequireAuthorization();

        /// <summary>
        /// 添加成员（v1.6.3 新增代理，管理员；透传 API POST /api/merchant/staff——
        /// 按手机号/邮箱查注册用户，未注册 400 文案透传）
        /// </summary>
        group.MapPost("/staff", async (
            AddStaffRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            // 改动说明（v1.6.4 邀请确认制）：透传 API 真实文案（"邀请已发送，对方同意后加入"等），
            //   原固定"成员已添加"与静默入伙语义一并废弃
            var result = await api.PostWithResultAsync<AddStaffResponse>("/api/merchant/staff", body, token, ct);
            if (result.Success)
                return Results.Ok(new { success = true, message = result.Data?.Message ?? "邀请已发送，对方同意后加入" });
            return Results.Json(new { success = false, message = result.ErrorText ?? "添加失败" },
                statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("AddMerchantStaff")
        .WithSummary("添加商户成员")
        .WithDescription("管理员按手机号/邮箱邀请用户，已注册用户转为待确认邀请（v2.9.0 邀请确认制）")
        .RequireAuthorization();

        /// <summary>
        /// 待我确认的员工邀请列表（v1.6.4 代理，透传 API GET /api/merchant/staff/invitations/pending）
        /// </summary>
        group.MapGet("/staff/invitations/pending", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.GetAsync<List<PendingStaffInvitationItem>>("/api/merchant/staff/invitations/pending", token, ct);
            return Results.Ok(result ?? []);
        })
        .WithName("GetPendingStaffInvitations")
        .WithSummary("待我确认的员工邀请")
        .WithDescription("被邀人查看发给自己的商户邀请（按 JWT 手机号匹配）")
        .RequireAuthorization();

        /// <summary>
        /// 接受员工邀请（v1.6.4 代理；API 400/401 业务原因透传给横幅操作反馈）
        /// </summary>
        group.MapPost("/staff/invitations/{invitationId}/accept", async (
            string invitationId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>($"/api/merchant/staff/invitations/{invitationId}/accept", new { }, token, ct);
            if (result.Success)
                return Results.Ok(new { success = true, message = "已接受邀请，正式加入商户" });
            return Results.Json(new { success = false, message = result.ErrorText ?? "接受失败" },
                statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("AcceptStaffInvitation")
        .WithSummary("接受员工邀请")
        .WithDescription("被邀人同意加入商户")
        .RequireAuthorization();

        /// <summary>
        /// 拒绝员工邀请（v1.6.4 代理）
        /// </summary>
        group.MapPost("/staff/invitations/{invitationId}/decline", async (
            string invitationId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>($"/api/merchant/staff/invitations/{invitationId}/decline", new { }, token, ct);
            if (result.Success)
                return Results.Ok(new { success = true, message = "已拒绝邀请" });
            return Results.Json(new { success = false, message = result.ErrorText ?? "操作失败" },
                statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("DeclineStaffInvitation")
        .WithSummary("拒绝员工邀请")
        .WithDescription("被邀人拒绝商户邀请")
        .RequireAuthorization();

        /// <summary>
        /// 撤销员工邀请（v1.6.4 代理，管理员撤回待确认邀请）
        /// </summary>
        group.MapPost("/staff/invitations/{invitationId}/revoke", async (
            string invitationId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>($"/api/merchant/staff/invitations/{invitationId}/revoke", new { }, token, ct);
            if (result.Success)
                return Results.Ok(new { success = true, message = "邀请已撤销" });
            return Results.Json(new { success = false, message = result.ErrorText ?? "撤销失败" },
                statusCode: result.StatusCode > 0 ? result.StatusCode : 502);
        })
        .WithName("RevokeStaffInvitation")
        .WithSummary("撤销员工邀请")
        .WithDescription("商户管理员撤回待确认的员工邀请")
        .RequireAuthorization();

        /// <summary>
        /// 移除成员（v1.6.3 新增代理，管理员；透传 API DELETE /api/merchant/staff/{userId}）
        /// </summary>
        group.MapDelete("/staff/{userId}", async (
            string userId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var ok = await api.DeleteVoidAsync($"/api/merchant/staff/{userId}", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "成员已移除" : "移除失败（权限不足或成员不存在）" });
        })
        .WithName("RemoveMerchantStaff")
        .WithSummary("移除商户成员")
        .WithDescription("从当前商户移除成员（需商户管理员权限）")
        .RequireAuthorization();

        /// <summary>
        /// 停用成员（管理员）
        /// </summary>
        group.MapPost("/members/{userId}/suspend", async (
            string userId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/merchant/members/{userId}/suspend", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "成员已停用" : "停用失败" });
        })
        .WithName("SuspendMerchantMember")
        .WithSummary("停用成员")
        .WithDescription("停用商户成员（需商家管理员权限）")
        .RequireAuthorization();

        /// <summary>
        /// 恢复被停用成员（管理员）
        /// </summary>
        group.MapPost("/members/{userId}/activate", async (
            string userId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/merchant/members/{userId}/activate", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "成员已恢复" : "恢复失败" });
        })
        .WithName("ActivateMerchantMember")
        .WithSummary("恢复成员")
        .WithDescription("恢复被停用的商户成员（需商家管理员权限）")
        .RequireAuthorization();

        /// <summary>
        /// 变更成员角色（管理员）
        /// </summary>
        group.MapPut("/members/{userId}/role", async (
            string userId,
            ChangeMemberRoleRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            var ok = await api.PutVoidAsync($"/api/merchant/members/{userId}/role", body, token, ct);
            return Results.Ok(new { success = ok, message = ok ? "角色已变更" : "变更失败" });
        })
        .WithName("ChangeMerchantMemberRole")
        .WithSummary("变更成员角色")
        .WithDescription("变更商户成员角色（需商家管理员权限）")
        .RequireAuthorization();
    }

    /// <summary>
    /// 从入站请求提取用户 access token（与 MeEndpoints 同逻辑）
    /// </summary>
    private static string? GetToken(HttpContext http)
    {
        if (string.IsNullOrEmpty(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value))
            return null;
        return http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
    }

    // ============ 参数 ============

    public class SearchParams
    {
        public string? Keyword { get; set; }
        public bool? VerifiedOnly { get; set; }
        // 改动说明：新增排序透传（name/productcount），与 API 商家搜索对齐
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class BearingQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// 入驻申请请求体（对齐 API ApplyMerchantRequest；v1.6.0 LicenseUrl 泛化为 Documents 材料集合）
    /// </summary>
    public record ApplyRequest(
        string? Mode = "self",
        Guid? ClaimMerchantId = null,
        string? Name = null,
        int? Type = null,
        string? ContactPerson = null,
        string? Phone = null,
        string? Mobile = null,
        string? Email = null,
        string? Address = null,
        string? CompanyName = null,
        string? UnifiedSocialCreditCode = null,
        string? Description = null,
        IReadOnlyList<DocumentInput>? Documents = null);

    /// <summary>
    /// 随单证照材料项（v1.6.0 对齐 API DocumentSubmission：type 1 执照 / 2 授权书 / 3 厂房照）
    /// </summary>
    public record DocumentInput(int Type, string FileUrl);

    /// <summary>
    /// 入驻申请成功响应 data（对齐 API /api/merchant/apply 的 {merchantId, message}）
    /// </summary>
    public sealed record ApplyResultData(string? MerchantId, string? Message);

    /// <summary>
    /// 提名他人为管理员请求体（对齐 API NominateMerchantRequest）
    /// </summary>
    public record NominateRequest(
        string? NomineePhone = null,
        string? NomineeEmail = null,
        string? Name = null,
        int? Type = null,
        string? CompanyName = null,
        string? ContactPerson = null,
        string? Phone = null,
        string? Mobile = null,
        string? Email = null,
        string? Address = null,
        bool InitiatorJoins = true,
        Guid? TargetMerchantId = null);

    /// <summary>
    /// 接受提名请求体（对齐 API AcceptNominationRequest；v1.6.0 LicenseUrl 泛化为 Documents）
    /// </summary>
    public record AcceptNominationRequest(
        string? ContactPerson = null,
        string? Phone = null,
        string? Mobile = null,
        string? Email = null,
        string? Address = null,
        string? CompanyName = null,
        string? UnifiedSocialCreditCode = null,
        string? Description = null,
        IReadOnlyList<DocumentInput>? Documents = null);

    /// <summary>
    /// 被拒重提请求体（对齐 API ResubmitApplicationRequest，v1.5.0 新增；v1.6.0 LicenseUrl 泛化为 Documents）
    /// </summary>
    public record ResubmitRequest(
        string? Name = null,
        int? Type = null,
        string? ContactPerson = null,
        string? Phone = null,
        string? Mobile = null,
        string? Email = null,
        string? Address = null,
        string? CompanyName = null,
        string? UnifiedSocialCreditCode = null,
        string? Description = null,
        IReadOnlyList<DocumentInput>? Documents = null);

    /// <summary>
    /// 入驻申请详情项（对齐 API MerchantApplicationDetailDto，v1.5.0 新增；v1.6.0 补材料清单）
    /// </summary>
    public record ApplicationDetailItem(
        Guid MerchantId, string MerchantName, string Status,
        string? RejectReason, string ApplicationMode, string Role, int Type,
        string? CompanyName, string? UnifiedSocialCreditCode,
        string? ContactPerson, string? Phone, string? Mobile, string? Email, string? Address,
        string? Description, string? LogoUrl,
        IReadOnlyList<ApplicationDocumentItem>? Documents = null);

    /// <summary>
    /// 申请随单材料项（对齐 API ApplicationDocumentDto，v1.6.0 新增）
    /// </summary>
    public record ApplicationDocumentItem(
        int Type, string TypeName, string FileUrl, string Status, string? ReviewComment);

    // ============ DTO ============

    public record MerchantItem(
        Guid Id, string Name, string? CompanyName, string? Type,
        bool IsVerified, string? Status, int? ProductCount, string? LogoUrl);

    // 对齐 API MerchantDto 字段（补 LogoUrl/ContactPerson/Mobile/Email/Address/Grade/FollowerCount/ProductCount）
    public record MerchantDetail(
        Guid Id, string Name, string? CompanyName, string? Type,
        string? ContactPerson, string? Phone, string? Mobile, string? Email, string? Address,
        bool IsVerified, string? Status, string? Grade,
        int FollowerCount, int ProductCount, string? LogoUrl);

    public record MerchantBearingItem(
        Guid BearingId, string BearingPartNumber, string? OldNumber,
        string? BearingTypeName, string? BrandName,
        decimal? InnerDiameter, decimal? OuterDiameter, decimal? Width,
        string? Price, bool IsOnSale);

    /// <summary>
    /// 入驻状态项（对齐 API MerchantApplicationDto）
    /// 改动说明（v1.6.3）：加 VerifyRequested 透传（Taro"申请认证"按钮态）
    /// </summary>
    public record MerchantApplicationItem(
        Guid MerchantId, string MerchantName, string Status,
        string? RejectReason, string Role, bool IsVerified, string? LogoUrl, bool VerifyRequested);

    /// <summary>
    /// 入驻发现搜索项（对齐 API ClaimableMerchantDto；v1.6.4 全量匹配 + 认领可行性标记）
    /// </summary>
    public record ClaimableMerchantItem(
        Guid Id,
        string Name,
        string? CompanyName,
        string Type,
        bool IsClaimable,
        bool IsMine,
        string StatusText);

    /// <summary>
    /// 待接受提名项（对齐 API PendingNominationDto）
    /// </summary>
    public record PendingNominationItem(
        string InvitationCode,
        Guid MerchantId,
        string MerchantName,
        string? CompanyName,
        DateTime CreatedAt);

    /// <summary>
    /// 商户成员项（对齐 API MerchantStaffDto）
    /// 改动说明（v1.6.3）：加 IsSelf——Taro 登录态 id 是 Identity sub 与成员 UserId 不同源，
    ///   前端无法自判本人行，由 API 权威标记透传。
    /// 改动说明（v1.6.4）：加 InvitationId——Status=Invited 行为待确认邀请（v2.9.0 邀请确认制），
    ///   撤销操作用此 ID（此类行 Id 为空 Guid，无成员可操作）
    /// 改动说明（v1.6.5）：加 Mobile——成员详情面板展示手机号（API 侧仅本商户成员列表端点返回）
    /// </summary>
    public record MerchantStaffItem(Guid Id, string Nickname, string? Avatar, string? Role, string Status, bool IsSelf, Guid? InvitationId, string? Mobile, DateTime? JoinedAt);

    /// <summary>
    /// 待我确认的员工邀请项（v1.6.4，对齐 API PendingStaffInvitationDto；商户页横幅消费）
    /// </summary>
    public record PendingStaffInvitationItem(
        Guid InvitationId,
        Guid MerchantId,
        string MerchantName,
        string Role,
        string? InvitedByName,
        DateTime CreatedAt);

    /// <summary>
    /// 变更成员角色请求体
    /// </summary>
    public record ChangeMemberRoleRequest(string Role);

    /// <summary>
    /// 添加成员请求体（对齐 API AddStaffCommand：手机号/邮箱二选一 + 角色）
    /// </summary>
    public record AddStaffRequest(string? Phone, string? Email, string? Role);

    /// <summary>
    /// 添加成员响应（v1.6.4：透传 API AddStaffResult.Message 真实文案）
    /// </summary>
    public record AddStaffResponse(string? Message);

    // ============ 工具 ============

    private static string BuildQueryString(string path, SearchParams p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(p.Keyword)) parts.Add($"keyword={Uri.EscapeDataString(p.Keyword)}");
        if (p.VerifiedOnly.HasValue) parts.Add($"verifiedOnly={p.VerifiedOnly.Value.ToString().ToLower()}");
        if (!string.IsNullOrEmpty(p.SortBy)) parts.Add($"sortBy={Uri.EscapeDataString(p.SortBy)}");
        if (!string.IsNullOrEmpty(p.SortOrder)) parts.Add($"sortOrder={Uri.EscapeDataString(p.SortOrder)}");
        parts.Add($"page={p.Page}");
        parts.Add($"pageSize={p.PageSize}");
        return path + "?" + string.Join("&", parts);
    }
}
