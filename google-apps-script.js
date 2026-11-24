function doPost(e) {
  try {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.getSheetByName("Purchases") || ss.getSheets()[0];
    var data = JSON.parse(e.postData.contents);
    var action = data.action;

    // Ensure Headers exist
    if (sheet.getLastRow() === 0) {
      sheet.appendRow(["ID", "Item Name", "Provider", "Total Amount", "Remaining Amount", "Installments", "Status", "Purchase Date", "Last Updated"]);
    }

    var rows = sheet.getDataRange().getValues();

    // 1. ADD PURCHASE
    if (action === "add_purchase") {
      sheet.appendRow([
        data.id,
        data.itemName,
        data.provider,
        data.totalAmount,
        data.remainingAmount,
        data.installments,
        data.status,
        data.purchaseDate,
        new Date().toISOString()
      ]);
      return createResponse({ status: "success", message: "Purchase added successfully." });
    }

    // 2. EDIT / UPDATE PURCHASE
    if (action === "edit_purchase") {
      for (var i = 1; i < rows.length; i++) {
        if (rows[i][0] == data.id || (data.oldItemName && rows[i][1] === data.oldItemName)) {
          var rowIndex = i + 1;
          if (data.itemName) sheet.getRange(rowIndex, 2).setValue(data.itemName);
          if (data.provider) sheet.getRange(rowIndex, 3).setValue(data.provider);
          if (data.totalAmount !== undefined) sheet.getRange(rowIndex, 4).setValue(data.totalAmount);
          if (data.remainingAmount !== undefined) sheet.getRange(rowIndex, 5).setValue(data.remainingAmount);
          if (data.installments !== undefined) sheet.getRange(rowIndex, 6).setValue(data.installments);
          if (data.status) sheet.getRange(rowIndex, 7).setValue(data.status);
          sheet.getRange(rowIndex, 9).setValue(new Date().toISOString());
          return createResponse({ status: "updated", message: "Purchase updated successfully." });
        }
      }
      return createResponse({ status: "not_found", message: "Item not found for update." });
    }

    // 3. RECORD PAYMENT (Update Remaining Balance & Status)
    if (action === "record_payment") {
      for (var i = 1; i < rows.length; i++) {
        if (rows[i][0] == data.purchaseId || (data.itemName && rows[i][1] === data.itemName)) {
          var rowIndex = i + 1;
          var curRemaining = parseFloat(sheet.getRange(rowIndex, 5).getValue()) || 0;
          var newRemaining = Math.max(0, curRemaining - parseFloat(data.amountPaid));
          sheet.getRange(rowIndex, 5).setValue(newRemaining);

          if (newRemaining <= 0) {
            sheet.getRange(rowIndex, 7).setValue("Completed");
          }
          sheet.getRange(rowIndex, 9).setValue(new Date().toISOString());
          return createResponse({ status: "payment_recorded", newRemaining: newRemaining });
        }
      }
      return createResponse({ status: "not_found" });
    }

    // 4. DELETE PURCHASE
    if (action === "delete_purchase") {
      for (var i = rows.length - 1; i >= 1; i--) {
        if (rows[i][0] == data.id || (data.itemName && rows[i][1] === data.itemName)) {
          sheet.deleteRow(i + 1);
          return createResponse({ status: "deleted", message: "Purchase deleted successfully." });
        }
      }
      return createResponse({ status: "not_found" });
    }

    return createResponse({ status: "unknown_action" });

  } catch (err) {
    return createResponse({ status: "error", message: err.toString() });
  }
}

function doGet() {
  try {
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var sheet = ss.getSheetByName("Purchases") || ss.getSheets()[0];
    var rows = sheet.getDataRange().getValues();
    return createResponse(rows);
  } catch (err) {
    return createResponse({ error: err.toString() });
  }
}

function createResponse(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj))
                       .setMimeType(ContentService.MimeType.JSON);
}
