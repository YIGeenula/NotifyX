/**
 * NotifyX - Centralized Multi-Tenant Cloud Vault & Webhook API
 * Automatically manages Purchases and Payments in Google Sheets with Vault Isolation.
 * 
 * Each user has a unique Vault ID (e.g. NX-A8291F) generated securely by the desktop app.
 * Only data matching the requested Vault ID is returned to the user's Web Dashboard.
 * 100% Free - Serverless - Zero Operational Cost.
 */

function doPost(e) {
  try {
    if (!e || !e.postData || !e.postData.contents) {
      return createResponse({ status: "error", message: "No post data received." });
    }

    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.getSheetByName("Purchases");
    if (!sheet) {
      sheet = ss.insertSheet("Purchases");
    }

    // Ensure Headers exist (Column 1 is Vault ID)
    if (sheet.getLastRow() === 0) {
      sheet.appendRow(["Vault ID", "ID", "Item Name", "Provider", "Total Amount", "Remaining Amount", "Installments", "Status", "Purchase Date", "Last Updated"]);
      sheet.getRange("A1:J1").setFontWeight("bold").setBackground("#7C5CFC").setFontColor("#FFFFFF");
      sheet.setFrozenRows(1);
    }

    var data = JSON.parse(e.postData.contents);
    var action = data.action;
    var vaultId = (data.vaultId || "DEFAULT").toUpperCase().trim();
    var rows = sheet.getDataRange().getValues();

    // 1. ADD PURCHASE
    if (action === "add_purchase") {
      sheet.appendRow([
        vaultId,
        data.id,
        data.itemName,
        data.provider,
        Number(data.totalAmount) || 0,
        Number(data.remainingAmount) || 0,
        Number(data.installments) || 1,
        data.status || "Active",
        data.purchaseDate || new Date().toISOString().substring(0, 10),
        new Date().toISOString()
      ]);
      return createResponse({ status: "success", message: "Purchase added to vault." });
    }

    // 2. EDIT / UPDATE PURCHASE
    if (action === "edit_purchase") {
      for (var i = 1; i < rows.length; i++) {
        var rowVault = String(rows[i][0]).toUpperCase().trim();
        var rowId = rows[i][1];
        var rowItemName = rows[i][2];

        if (rowVault === vaultId && (rowId == data.id || (data.oldItemName && rowItemName === data.oldItemName))) {
          var rowIndex = i + 1;
          if (data.itemName) sheet.getRange(rowIndex, 3).setValue(data.itemName);
          if (data.provider) sheet.getRange(rowIndex, 4).setValue(data.provider);
          if (data.totalAmount !== undefined) sheet.getRange(rowIndex, 5).setValue(Number(data.totalAmount) || 0);
          if (data.remainingAmount !== undefined) sheet.getRange(rowIndex, 6).setValue(Number(data.remainingAmount) || 0);
          if (data.installments !== undefined) sheet.getRange(rowIndex, 7).setValue(Number(data.installments) || 1);
          if (data.status) sheet.getRange(rowIndex, 8).setValue(data.status);
          sheet.getRange(rowIndex, 10).setValue(new Date().toISOString());
          return createResponse({ status: "updated", message: "Purchase updated successfully." });
        }
      }
      // If not found, append
      sheet.appendRow([
        vaultId,
        data.id,
        data.itemName,
        data.provider,
        Number(data.totalAmount) || 0,
        Number(data.remainingAmount) || 0,
        Number(data.installments) || 1,
        data.status || "Active",
        data.purchaseDate || new Date().toISOString().substring(0, 10),
        new Date().toISOString()
      ]);
      return createResponse({ status: "added_fallback", message: "Purchase added to vault." });
    }

    // 3. RECORD PAYMENT
    if (action === "record_payment") {
      for (var i = 1; i < rows.length; i++) {
        var rowVault = String(rows[i][0]).toUpperCase().trim();
        var rowId = rows[i][1];
        var rowItemName = rows[i][2];

        if (rowVault === vaultId && (rowId == data.purchaseId || (data.itemName && rowItemName === data.itemName))) {
          var rowIndex = i + 1;
          var curRemaining = parseFloat(sheet.getRange(rowIndex, 6).getValue()) || 0;
          var newRemaining = Math.max(0, curRemaining - parseFloat(data.amountPaid));
          sheet.getRange(rowIndex, 6).setValue(newRemaining);

          if (newRemaining <= 0) {
            sheet.getRange(rowIndex, 8).setValue("Completed");
          }
          sheet.getRange(rowIndex, 10).setValue(new Date().toISOString());
          return createResponse({ status: "payment_recorded", newRemaining: newRemaining });
        }
      }
      return createResponse({ status: "not_found", message: "Payment item not found in vault." });
    }

    // 4. DELETE PURCHASE
    if (action === "delete_purchase") {
      for (var i = rows.length - 1; i >= 1; i--) {
        var rowVault = String(rows[i][0]).toUpperCase().trim();
        var rowId = rows[i][1];
        var rowItemName = rows[i][2];

        if (rowVault === vaultId && (rowId == data.id || (data.itemName && rowItemName === data.itemName))) {
          sheet.deleteRow(i + 1);
          return createResponse({ status: "deleted", message: "Purchase removed from vault." });
        }
      }
      return createResponse({ status: "not_found", message: "Item not found in vault." });
    }

    return createResponse({ status: "unknown_action" });

  } catch (err) {
    return createResponse({ status: "error", message: err.toString() });
  }
}

function doGet(e) {
  try {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.getSheetByName("Purchases") || ss.getSheets()[0];
    var rows = sheet.getDataRange().getValues();

    if (rows.length <= 1) {
      return createResponse([]);
    }

    // Check if a specific vaultId is requested: ?vault=NX-XXXX
    var targetVault = "";
    if (e && e.parameter && e.parameter.vault) {
      targetVault = e.parameter.vault.toUpperCase().trim();
    }

    var resultRows = [];
    // Standard headers for Web Dashboard: ID, Item Name, Provider, Total Amount, Remaining Amount, Installments, Status, Purchase Date
    resultRows.push(["ID", "Item Name", "Provider", "Total Amount", "Remaining Amount", "Installments", "Status", "Purchase Date"]);

    for (var i = 1; i < rows.length; i++) {
      var row = rows[i];
      var rowVault = String(row[0]).toUpperCase().trim();

      // If targetVault is provided, filter ONLY rows belonging to that vault!
      if (targetVault && rowVault !== targetVault) {
        continue;
      }

      // Append clean row without exposing vault column
      resultRows.push([
        row[1], // ID
        row[2], // Item Name
        row[3], // Provider
        row[4], // Total Amount
        row[5], // Remaining Amount
        row[6], // Installments
        row[7], // Status
        row[8]  // Purchase Date
      ]);
    }

    return createResponse(resultRows);
  } catch (err) {
    return createResponse({ error: err.toString() });
  }
}

function createResponse(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj))
                       .setMimeType(ContentService.MimeType.JSON);
}
